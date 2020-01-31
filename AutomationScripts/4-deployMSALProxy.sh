#!/bin/sh -x

echo "BEGIN @ $(date +"%T"): Deploy MSAL Proxy..."
cat << EOF > ../msal-proxy/templates/azure-files-storage-class.yaml
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

cat msal-proxy/templates/azure-files-storage-class.yaml

# kubectl apply -f azure-files-storage-class.yaml

cat << EOF > ../msal-proxy/templates/data-protection-persistent-claim.yaml
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

cat msal-proxy/templates/data-protection-persistent-claim.yaml

# kubectl apply -f data-protection-persistent-claim.yaml

cat << EOF > ../azure-pvc-roles.yaml
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

cat << EOF > ../msal-proxy/templates/msal-net-proxy.yaml
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
      -  image: "{{ .Values.image.repository }}:{{ .Values.image.tag }}"
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