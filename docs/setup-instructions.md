# K8 Auth Inject - Documentation Duplicate

## Prerequisites

These are **critical dependencies to install prior** to running the commands below.

- [JQ](https://stedolan.github.io/jq/)
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest)
    - Important! Make sure you are running the LATEST version of the Azure CLI.
- [Kubectl](https://kubernetes.io/docs/tasks/tools/install-kubectl/)
- [Helm 3](https://helm.sh/docs/intro/install/)

## Set Variables

Review these very carefully and modify.

    # Important! Set the name for your Azure AD App Registration. This will also be used for the Ingress DNS hostname.
    AD_APP_NAME="$USER-msal-proxy"
    
    # Set your AKS cluster name and resource group
    CLUSTER_NAME=msal-proxy-aks
    CLUSTER_RG=msal-proxy-rg
    
    # Set the email address for the cluster certificate issuer
    EMAIL=example@microsoft.com
    
    # Region to create resources
    LOCATION=southcentralus
    
    APP_HOSTNAME="$AD_APP_NAME.$LOCATION.cloudapp.azure.com"
    HOMEPAGE=https://$APP_HOSTNAME
    IDENTIFIER_URIS=$HOMEPAGE
    REPLY_URLS=https://$APP_HOSTNAME/msal/signin-oidc

## Login to Azure

    az login
    az account set -s "<the azure subscription you want to deploy this in.>"

## Create AKS Cluster

Note: It takes several minutes to create the AKS cluster. Complete these steps before proceeding to the next section.

    az group create -n $CLUSTER_RG -l $LOCATION
    az aks create -g $CLUSTER_RG -n $CLUSTER_NAME --vm-set-type VirtualMachineScaleSets --generate-ssh-keys --enable-managed-identity
    az aks get-credentials -g $CLUSTER_RG -n $CLUSTER_NAME
    
    # Important! Wait for the steps above to complete before proceeding.
    
## Install Helm

    #Add stable repo to Helm 3
    helm repo add stable https://charts.helm.sh/stable

## Install NGINX Ingress

    # Add ingress-controllers namespace
    kubectl create namespace ingress-controllers
    # Add Nginx to the helm repo
    helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
    helm repo update
    # Install the ingress controller
    helm install nginx-ingress ingress-nginx/ingress-nginx --namespace ingress-controllers --set rbac.create=true
    
    # Important! It take a few minutes for Azure to assign a public IP address to the ingress. Run this command until it returns a public IP address.
    kubectl get services/nginx-ingress-controller -n ingress-controllers -o jsonpath="{.status.loadBalancer.ingress[0].ip}"

## Configure DNS for the cluster public IP

To use AAD authentication for your application, you must use a FQDN with HTTPS.  For this tutorial, we will add a DNS record to the Ingress Public IP address.

```
# Get the AKS MC_ resource group name
NODE_RG=$(az aks show -n $CLUSTER_NAME -g $CLUSTER_RG -o json | jq -r '.nodeResourceGroup')
echo $NODE_RG

INGRESS_IP=$(kubectl get services/nginx-ingress-ingress-nginx-controller -n ingress-controllers -o jsonpath="{.status.loadBalancer.ingress[0].ip}")
echo $INGRESS_IP

IP_NAME=$(az network public-ip list -g $NODE_RG -o json | jq -c ".[] | select(.ipAddress | contains(\"$INGRESS_IP\"))" | jq '.name' -r)
echo $IP_NAME

# Add a DNS name ($AD_APP_NAME) to the public IP address
az network public-ip update -g $NODE_RG -n $IP_NAME --dns-name $AD_APP_NAME

# Get the FQDN assigned to the public IP address
INGRESS_HOST=$(az network public-ip show -g $NODE_RG -n $IP_NAME -o json | jq -r '.dnsSettings.fqdn')
echo $INGRESS_HOST
# This should be the same as the $APP_HOSTNAME
```

## Register AAD Application

```
# The default app created has permissions we don't need and can cause problem if you are in a more restricted tenant environment
# Copy/paste the entire snippet BELOW (and then press ENTER) to create the manifest.json file
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
# End of snippet to copy/paste

# Important! Review the file and check the values.
cat manifest.json

# Create the Azure AD SP for our application and save the Client ID to a variable
CLIENT_ID=$(az ad app create --display-name $AD_APP_NAME --homepage $HOMEPAGE --reply-urls $REPLY_URLS --required-resource-accesses @manifest.json -o json | jq -r '.appId')
echo $CLIENT_ID

OBJECT_ID=$(az ad app show --id $CLIENT_ID -o json | jq '.objectId' -r)
echo "OBJECT_ID: " $OBJECT_ID

az ad app update --id $OBJECT_ID --set oauth2Permissions[0].isEnabled=false
az ad app update --id $OBJECT_ID --set oauth2Permissions=[]

# The newly registered app does not have a password.  Use "az ad app credential reset" to add password and save to a variable.
CLIENT_SECRET=$(az ad app credential reset --id $CLIENT_ID -o json | jq '.password' -r)
echo $CLIENT_SECRET

# Get your Azure AD tenant ID and save to variable
AZURE_TENANT_ID=$(az account show -o json | jq '.tenantId' -r)
echo $AZURE_TENANT_ID
```

## Deploy MSAL Proxy

```
kubectl create secret generic aad-secret \
  --from-literal=AZURE_TENANT_ID=$AZURE_TENANT_ID \
  --from-literal=CLIENT_ID=$CLIENT_ID \
  --from-literal=CLIENT_SECRET=$CLIENT_SECRET
helm install msal-proxy ./charts/msal-proxy 

# Confirm everything was deployed.
kubectl get svc,deploy,pod
```


## Install Cert Manager

We will use Cert-Manager to create and install SSL Certs from Let'sEncrypt onto our K8S Cluster.  This is because AAD requires all endpoints be either [localhost](http://localhost) or HTTPS. 

Inspired by [https://docs.microsoft.com/en-us/azure/aks/ingress-tls](https://docs.microsoft.com/en-us/azure/aks/ingress-tls).

### Deploy Production Cert Manager

```
# Set the secret name
TLS_SECRET_NAME=$APP_HOSTNAME-tls

# Create the namespace 
kubectl create namespace cert-manager

# Add the Jetstack Helm repository
helm repo add jetstack https://charts.jetstack.io

# Update your local Helm chart repository cache
helm repo update

# Install the cert manager
helm install \
  cert-manager jetstack/cert-manager \
  --namespace cert-manager \
  --version v1.3.1 \
  --set installCRDs=true \
  --set ingressShim.defaultIssuerName=letsencrypt-prod \
  --set ingressShim.defaultIssuerKind=ClusterIssuer

# Make sure the cert-manager pods have started BEFORE proceeding. It can take 2-3 min for the cert-manager-webhook container to start up
kubectl get pods -n cert-manager

# Copy/paste the entire snippet BELOW (and then press ENTER) to create the cluster-issuer-prod.yaml file
cat << EOF > ./K8s-Config/cluster-issuer-prod.yaml
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
# End of snippet to copy/paste

# Important! Review the file and check the values.
cat ./K8s-Config/cluster-issuer-prod.yaml

# Deploy the issuer config to the cluster
kubectl apply -f ./K8s-Config/cluster-issuer-prod.yaml
```

## Deploy the Application

For this sample app, we will use [kuard](https://github.com/kubernetes-up-and-running/kuard).

To enable MSAL, the application will need two ingress rules.
  * Ingress for the application.  This needs HTTPS and some annotations to use the nginx auth plugin
  * Ingress for the MSAL Proxy.  This is for the `/msal` path for the same host

```
kubectl run kuard-pod --image=gcr.io/kuar-demo/kuard-amd64:1 --expose --port=8080

cat << EOF > ./K8s-Config/kuard-ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: kuard
  annotations:
    nginx.ingress.kubernetes.io/auth-url: "https://\$host/msal/auth"
    nginx.ingress.kubernetes.io/auth-signin: "https://\$host/msal/index?rd=\$escaped_request_uri"
    nginx.ingress.kubernetes.io/auth-response-headers: "x-injected-aio,x-injected-name,x-injected-nameidentifier,x-injected-objectidentifier,x-injected-preferred_username,x-injected-tenantid,x-injected-uti"
    kubernetes.io/tls-acme: "true"
    certmanager.k8s.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/rewrite-target: /\$1
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - $APP_HOSTNAME
    secretName: $TLS_SECRET_NAME
  rules:
  - host: $APP_HOSTNAME
    http:
      paths:
      - backend:
          service:
            name: kuard-pod
            port:
              number: 8080
        path: /(.*)
        pathType: ImplementationSpecific
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: msal-proxy
spec:
  rules:
  - host: $APP_HOSTNAME
    http:
      paths:
      - backend:
          service:
            name: msal-proxy
            port: 
              number: 80
        path: /msal
        pathType: ImplementationSpecific
  tls:
  - hosts:
    - $APP_HOSTNAME
    secretName: $TLS_SECRET_NAME
EOF

# End of snippet to copy/paste

# Important! Review the file and check the values.
cat ./K8s-Config/kuard-ingress.yaml

# Deploy the ingress config to the cluster
kubectl apply -f ./K8s-Config/kuard-ingress.yaml
```

## Verify Production Certificate works

After creating the ingress resource, this will initiate the Let's Encrypt certificate request process.   

```
# Verify certificate - this may take a few minutes to show the cert as ready=true
kubectl get certificate $TLS_SECRET_NAME
```

It should look something like this:

    NAME                  READY   SECRET                AGE
    ingress-tls-prod      True    ingress-tls-prod      5m

## Verify application works in browser

    # Load the homepage in the browser
    echo $HOMEPAGE

## Clean-up (optional)

    az ad app delete --id $CLIENT_ID
    helm delete nginx-ingress --purge
    helm delete cert-manager --purge
    helm delete msal-proxy --purge
    kubectl delete secret ingress-tls-prod
    kubectl delete -f https://raw.githubusercontent.com/jetstack/cert-manager/release-0.11/deploy/manifests/00-crds.yaml
    kubectl delete ns cert-manager

## Automated Scripts [Azure Cloud Shell] (optional)

- Go to the root folder
- Run bash command
```
# Run -h for all required and optional flags
bash main.sh -h

# Example Command
bash main.sh -a msal-test -c cluster-test -r easy-auth -e email@microsoft.com -d microsoft.com -l eastus
```

# References

- [https://github.com/kubernetes-up-and-running/kuard](https://github.com/kubernetes-up-and-running/kuard)
- [https://docs.cert-manager.io/en/latest/getting-started/install/kubernetes.html](https://docs.cert-manager.io/en/latest/getting-started/install/kubernetes.html)
- [https://kubernetes.github.io/ingress-nginx/examples/auth/oauth-external-auth](https://kubernetes.github.io/ingress-nginx/examples/auth/oauth-external-auth)

