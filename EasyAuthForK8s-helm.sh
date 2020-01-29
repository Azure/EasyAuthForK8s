#!/bin/sh -x
echo ""
echo "BEGIN @ $(date +"%T"): START OF SCRIPT"
echo ""
echo "BEGIN @ $(date +"%T"): Set variables..."

ITERATION=30
AD_APP_NAME="$USER-msal-proxy$ITERATION"
CLUSTER_NAME=msal-proxy$ITERATION
CLUSTER_RG=msal-proxyrg$ITERATION
EMAIL=kgates@microsoft.com
EMAIL_DOMAIN=microsoft.com
LOCATION=southcentralus
APP_HOSTNAME="$AD_APP_NAME.$LOCATION.cloudapp.azure.com"
HOMEPAGE=https://$APP_HOSTNAME
IDENTIFIER_URIS=$HOMEPAGE
REPLY_URLS=https://$APP_HOSTNAME/msal/signin-oidc
COOKIE_SECRET=$(python -c 'import os,base64; print(base64.b64encode(os.urandom(16)).decode("utf-8"))')
INGRESS_IP=0

echo "COOKIE_SECRET: " $COOKIE_SECRET
echo "COMPLETE @ $(date +"%T"): Setting variables"

echo "BEGIN @ $(date +"%T"): Creating the resource group..."
az group create -n $CLUSTER_RG -l $LOCATION
echo "COMPLETE @ $(date +"%T"): Resource group created"

echo "BEGIN @ $(date +"%T"): Creating the cluster..."
az aks create -g $CLUSTER_RG -n $CLUSTER_NAME --generate-ssh-keys --node-count 1
echo "COMPLETE @ $(date +"%T"): Cluster created!"

echo "BEGIN @ $(date +"%T"): Getting cluster creds..."
az aks get-credentials -g $CLUSTER_RG -n $CLUSTER_NAME
echo "COMPLETE @ $(date +"%T"): Getting cluster creds"

helm repo add stable https://kubernetes-charts.storage.googleapis.com

echo "BEGIN @ $(date +"%T"): Installing the ingress controller..."
kubectl create ns ingress-controllers
helm install nginx-ingress stable/nginx-ingress --namespace ingress-controllers --set rbac.create=true

INGRESS_IP=$(kubectl get services/nginx-ingress-controller -n ingress-controllers -o jsonpath="{.status.loadBalancer.ingress[0].ip}")

while [ "$INGRESS_IP" = "" ]
do
  echo "UPDATE @ $(date +"%T"): Checking for INGRESS_IP from Azure..."
  INGRESS_IP=$(kubectl get services/nginx-ingress-controller -n ingress-controllers -o jsonpath="{.status.loadBalancer.ingress[0].ip}")
  echo "UPDATE @ $(date +"%T"): Sleeping for 30 seconds..."
  sleep 5
done
echo "COMPLETE @ $(date +"%T"): INGRESS_IP is: " $INGRESS_IP

echo "BEGIN @ $(date +"%T"): Configure DNS for the cluster public IP..."
NODE_RG=$(az aks show -n $CLUSTER_NAME -g $CLUSTER_RG -o json | jq -r '.nodeResourceGroup')
echo "UPDATE @ $(date +"%T"): " $NODE_RG

INGRESS_IP=$(kubectl get services/nginx-ingress-controller -n ingress-controllers -o jsonpath="{.status.loadBalancer.ingress[0].ip}")
echo "UPDATE @ $(date +"%T"): " $INGRESS_IP

IP_NAME=$(az network public-ip list -g $NODE_RG -o json | jq -c ".[] | select(.ipAddress | contains(\"$INGRESS_IP\"))" | jq '.name' -r)
echo "UPDATE @ $(date +"%T"): " $IP_NAME

az network public-ip update -g $NODE_RG -n $IP_NAME --dns-name $AD_APP_NAME

INGRESS_HOST=$(az network public-ip show -g $NODE_RG -n $IP_NAME -o json | jq -r '.dnsSettings.fqdn')
echo "COMPLETE @ $(date +"%T"): INGRESS_HOST is: " $INGRESS_HOST

echo "BEGIN @ $(date +"%T"): Deploy sample app..."

kubectl run kuard-pod --image=gcr.io/kuar-demo/kuard-amd64:1 --expose --port=8080
echo "COMPLETE @ $(date +"%T"): Deployed sample app"

echo "BEGIN @ $(date +"%T"): Register AAD Application..."
cat << EOF > manifest.json
[
   {
      "resourceAccess" : [
         {
            "id" : "e1fe6dd8-ba31-4d61-89e7-88639da4683d",
            "type" : "Scope"
         }
      ],
      "resourceAppId" : "00000003-0000-0000-c000-000000000000"
   }
]
EOF

CLIENT_ID=$(az ad app create --display-name $AD_APP_NAME --homepage $HOMEPAGE --reply-urls $REPLY_URLS --required-resource-accesses @manifest.json -o json | jq -r '.appId')
echo "CLIENT_ID: " $CLIENT_ID

OBJECT_ID=$(az ad app show --id $CLIENT_ID -o json | jq '.objectId' -r)
echo "OBJECT_ID: " $OBJECT_ID
az ad app update --id $OBJECT_ID --set oauth2Permissions[0].isEnabled=false
az ad app update --id $OBJECT_ID --set oauth2Permissions=[]

CLIENT_SECRET=$(az ad app credential reset --id $CLIENT_ID -o json | jq '.password' -r)
echo "CLIENT_SECRET: " $CLIENT_SECRET

AZURE_TENANT_ID=$(az account show -o json | jq '.tenantId' -r)
echo "AZURE_TENANT_ID: " $AZURE_TENANT_ID

echo "COMPLETE @ $(date +"%T"): Register AAD Application"

echo "BEGIN @ $(date +"%T"): Deploy MSAL Proxy..."
cat << EOF > msal-proxy/templates/azure-files-storage-class.yaml
kind: StorageClass
apiVersion: storage.k8s.io/v1
metadata:
  name: azurefile
provisioner: kubernetes.io/azure-file
mountOptions:
  - dir_mode=0777
  - file_mode=0777
  - uid=1000
  - gid=1000
  - mfsymlinks
  - nobrl
  - cache=none
parameters:
  skuName: Standard_LRS
EOF

cat azure-files-storage-class.yaml

# kubectl apply -f azure-files-storage-class.yaml

cat << EOF > msal-proxy/templates/data-protection-persistent-claim.yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: msal-net-proxy-az-file-pv-claim
spec:
  accessModes:
    - ReadWriteMany
  storageClassName: azurefile
  resources:
    requests:
      storage: 5Gi
EOF

cat data-protection-persistent-claim.yaml

# kubectl apply -f data-protection-persistent-claim.yaml

cat << EOF > azure-pvc-roles.yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: system:azure-cloud-provider
rules:
- apiGroups: ['']
  resources: ['secrets']
  verbs:     ['get','create']
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: system:azure-cloud-provider
roleRef:
  kind: ClusterRole
  apiGroup: rbac.authorization.k8s.io
  name: system:azure-cloud-provider
subjects:
- kind: ServiceAccount
  name: persistent-volume-binder
  namespace: kube-system
EOF

cat azure-pvc-roles.yaml

kubectl apply -f azure-pvc-roles.yaml

cat << EOF > msal-proxy/templates/msal-net-proxy.yaml
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  labels:
    k8s-app: msal-net-proxy
  name: msal-net-proxy
spec:
  replicas: 2
  selector:
    matchLabels:
      k8s-app: msal-net-proxy
  template:
    metadata:
      labels:
        k8s-app: msal-net-proxy
    spec:
      containers:
      -  image: richtercloud/msal-net-proxy-opt:latest
         imagePullPolicy: Always
         name: msal-net-proxy
         env:
         -  name: DataProtectionFileLocation
            value: /mnt/dp
         -  name: ForceHttps
            value: "true"
         -  name: AzureAd__Instance
            value: https://login.microsoftonline.com/
         -  name: AzureAd__Domain
            value: microsoft.onmicrosoft.com
         -  name: AzureAd__TenantId
            value: $AZURE_TENANT_ID
         -  name: AzureAd__ClientId
            value: $CLIENT_ID
         -  name: AzureAd__CallbackPath
            value: /msal/signin-oidc
         -  name: AzureAd__SignedOutCallbackPath
            value: /msal/signout-callback-oidc
         -  name: AzureAd__ClientSecret
            value: $CLIENT_SECRET
         -  name: Logging__LogLevel__Default
            value: Debug
         -  name: AllowedHosts
            value: "*"
         -  name: RedirectParam
            value: rd
         -  name: ShowLogin
            value: "false"    
         ports:
         - containerPort: 80
           protocol: TCP
         volumeMounts:
         - mountPath: "/mnt/dp"
           name: dpvol
      volumes:
      - name: dpvol
        persistentVolumeClaim:
          claimName: msal-net-proxy-az-file-pv-claim
---
apiVersion: v1
kind: Service
metadata:
  labels:
    k8s-app: msal-net-proxy
  name: msal-net-proxy
spec:
  ports:
  - name: http
    port: 80
    protocol: TCP
    targetPort: 80
  selector:
    k8s-app: msal-net-proxy
EOF

cat msal-proxy/templates/msal-net-proxy.yaml

# kubectl apply -f msal-net-proxy.yaml

echo "BEGIN @ $(date +"%T"): Calling Helm..."
echo ""
helm install msal-proxy msal-proxy
echo ""
echo "COMPLETE @ $(date +"%T"): Calling Helm"

kubectl get svc,deploy,pod

INPUT_STRING=no
while [ "$INPUT_STRING" != "yes" ]
do
  echo ""
  kubectl get svc,deploy,pod
  echo ""
  echo "Did everything start OK? Type 'yes' or press enter to try again..."
  read INPUT_STRING
done

echo "COMPLETE @ $(date +"%T"): Deploy MSAL Proxy"

echo "BEGIN @ $(date +"%T"): Install Cert Manager..."
TLS_SECRET_NAME=ingress-tls-prod

kubectl create namespace cert-manager

kubectl apply -f https://raw.githubusercontent.com/jetstack/cert-manager/release-0.11/deploy/manifests/00-crds.yaml --validate=false

helm repo add jetstack https://charts.jetstack.io

helm repo update

helm install cert-manager --namespace cert-manager --set ingressShim.defaultIssuerName=letsencrypt-prod --set ingressShim.defaultIssuerKind=ClusterIssuer jetstack/cert-manager --version v0.11.0

kubectl get pods -n cert-manager

cat << EOF > cluster-issuer-prod.yaml
apiVersion: cert-manager.io/v1alpha2
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
  namespace: cert-manager
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: $EMAIL
    privateKeySecretRef:
      name: letsencrypt-prod
    # Add a single challenge solver, HTTP01 using nginx
    solvers:
    - http01:
        ingress:
          class: nginx
EOF

cat cluster-issuer-prod.yaml

INPUT_STRING=no
while [ "$INPUT_STRING" != "yes" ]
do
  echo ""
  kubectl get pods -n cert-manager  
  echo ""
  echo "Did the cert-manager pods start OK? Type 'yes' or press enter to try again..."
  read INPUT_STRING
done

kubectl apply -f cluster-issuer-prod.yaml

echo "COMPLETE @ $(date +"%T"): Install Cert Manager"

echo "BEGIN @ $(date +"%T"): Deploy the Ingress Resources..."
cat << EOF > hello-world-ingress.yaml
apiVersion: extensions/v1beta1
kind: Ingress
metadata:
  name: hello-world-ingress
  annotations:
    nginx.ingress.kubernetes.io/auth-url: "https://\$host/msal/auth"
    nginx.ingress.kubernetes.io/auth-signin: "https://\$host/msal/index?rd=\$escaped_request_uri"
    nginx.ingress.kubernetes.io/auth-response-headers: "x-injected-aio,x-injected-name,x-injected-nameidentifier,x-injected-objectidentifier,x-injected-preferred_username,x-injected-tenantid,x-injected-uti"
    kubernetes.io/ingress.class: nginx
    kubernetes.io/tls-acme: "true"
    certmanager.k8s.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/rewrite-target: /\$1
spec:
  tls:
  - hosts:
    - $APP_HOSTNAME
    secretName: $TLS_SECRET_NAME
  rules:
  - host: $APP_HOSTNAME
    http:
      paths:
      - backend:
          serviceName: kuard-pod
          servicePort: 8080
        path: /(.*)
---
apiVersion: extensions/v1beta1
kind: Ingress
metadata:
  name: msal-net-proxy
spec:
  rules:
  - host: $APP_HOSTNAME
    http:
      paths:
      - backend:
          serviceName: msal-net-proxy
          servicePort: 80
        path: /msal
  tls:
  - hosts:
    - $APP_HOSTNAME
    secretName: $TLS_SECRET_NAME
EOF

cat hello-world-ingress.yaml

kubectl apply -f hello-world-ingress.yaml

echo "COMPLETE @ $(date +"%T"): Deploy the Ingress Resources"
echo "BEGIN @ $(date +"%T"): Verify Production Certificate works..."
kubectl get certificate $TLS_SECRET_NAME
INPUT_STRING=no
while [ "$INPUT_STRING" != "yes" ]
do
  echo ""
  kubectl get certificate $TLS_SECRET_NAME
  echo ""
  echo "Is the certificate showing READY = True? Type 'yes' or press enter to try again..."
  read INPUT_STRING
done
echo "COMPLETE @ $(date +"%T"): Verify Production Certificate works"
echo ""
echo ""
echo "END OF SCRIPT"
echo ""
echo ""
echo "Visit the app in the browser. Good luck! " $HOMEPAGE
echo ""
echo ""
