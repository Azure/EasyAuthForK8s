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
    CLUSTER_NAME=msal-proxy
    CLUSTER_RG=msal-proxyrg
    
    # Set the email address for the cluster certificate issuer
    EMAIL=dakondra@microsoft.com
    
    # Set email domain for certificates
    EMAIL_DOMAIN=microsoft.com
    
    # Region to create resources
    LOCATION=southcentralus
    
    APP_HOSTNAME="$AD_APP_NAME.$LOCATION.cloudapp.azure.com"
    HOMEPAGE=https://$APP_HOSTNAME
    IDENTIFIER_URIS=$HOMEPAGE
    REPLY_URLS=https://$APP_HOSTNAME/msal/signin-oidc
    COOKIE_SECRET=$(python -c 'import os,base64; print(base64.b64encode(os.urandom(16)).decode("utf-8"))')
    echo $COOKIE_SECRET

## Login to Azure

    az login
    az account set -s "<the azure subscription you want to deploy this in.>"

## Create AKS Cluster

Note: It takes several minutes to create the AKS cluster. Complete these steps before proceeding to the next section.

    az group create -n $CLUSTER_RG -l $LOCATION
    az aks create -g $CLUSTER_RG -n $CLUSTER_NAME --vm-set-type VirtualMachineScaleSets --generate-ssh-keys
    az aks get-credentials -g $CLUSTER_RG -n $CLUSTER_NAME
    
    # Important! Wait for the steps above to complete before proceeding.
    
    
    # Important! If you get the error below when running the second line, run these two commands and then run az aks create.
    # "For Error: Operation failed with status: 'Bad Request'. Details: Service principal clientID: XXXXXX not found in Active Directory tenant XXXXXX, Please see [https://aka.ms/aks-sp-help](https://aka.ms/aks-sp-help) for more details."
    cd .azure/
    rm aksServicePrincipal.json
    cd ..
    az aks create -g $CLUSTER_RG -n $CLUSTER_NAME --vm-set-type VirtualMachineScaleSets
    
    # If the error persists, try it again.
    # For more information, you can go here: https://stackoverflow.com/questions/47516018/creating-a-kubernetes-cluster-in-azure-fails

## Install Helm

    #Add stable repo to Helm 3
    helm repo add stable https://kubernetes-charts.storage.googleapis.com

## Install NGINX Ingress

    # Install the ingress controller
    helm install stable/nginx-ingress nginx-ingress --namespace ingress-controllers --set rbac.create=true
    
    # Important! It take a few minutes for Azure to assign a public IP address to the ingress. Run this command until it returns a public IP address.
    kubectl get services/nginx-ingress-controller -n ingress-controllers -o jsonpath="{.status.loadBalancer.ingress[0].ip}"

## Configure DNS for the cluster public IP

Important! Ensure you have [JQ](https://stedolan.github.io/jq/) installed prior to running the commands below.

Set variables and update the DNS name on the IP address.

    # Get the AKS MC_ resource group name
    NODE_RG=$(az aks show -n $CLUSTER_NAME -g $CLUSTER_RG -o json | jq -r '.nodeResourceGroup')
    echo $NODE_RG
    
    INGRESS_IP=$(kubectl get services/nginx-ingress-controller -n ingress-controllers -o jsonpath="{.status.loadBalancer.ingress[0].ip}")
    echo $INGRESS_IP
    
    IP_NAME=$(az network public-ip list -g $NODE_RG -o json | jq -c ".[] | select(.ipAddress | contains(\"$INGRESS_IP\"))" | jq '.name' -r)
    echo $IP_NAME
    
    # Add a DNS name ($AD_APP_NAME) to the public IP address
    az network public-ip update -g $NODE_RG -n $IP_NAME --dns-name $AD_APP_NAME
    
    # Get the FQDN assigned to the public IP address
    INGRESS_HOST=$(az network public-ip show -g $NODE_RG -n $IP_NAME -o json | jq -r '.dnsSettings.fqdn')
    echo $INGRESS_HOST

## Deploy the application and create the ClusterIP Service

For this case we're using the Kubernetes Up and Running Daemon

[kubernetes-up-and-running/kuard](https://github.com/kubernetes-up-and-running/kuard)

    kubectl run kuard-pod --image=gcr.io/kuar-demo/kuard-amd64:1 --expose --port=8080

## Register AAD Application

    # Create the Azure AD App Registration and save the Client ID to a variable
    CLIENT_ID=$(az ad sp create-for-rbac --skip-assignment --display-name $AD_APP_NAME --homepage $HOMEPAGE --reply-urls $REPLY_URLS --required-resource-accesses @manifest.json -o json | jq -r '.appId')
    echo $CLIENT_ID
    
    # The newly registered app does not have a password.  Use "az ad app credential reset" to add password and save to a variable.
    CLIENT_SECRET=$(az ad app credential reset --id $CLIENT_ID -o json | jq '.password' -r)
    echo $CLIENT_SECRET
    
    # Get your Azure AD tenant ID and save to variable
    AZURE_TENANT_ID=$(az account show -o json | jq '.tenantId' -r)
    echo $AZURE_TENANT_ID

## Deploy MSAL Proxy

    # Copy/paste the entire snippet BELOW (and then press ENTER) to create the azure-files-storage-class.yaml file
    cat << EOF > azure-files-storage-class.yaml
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
    # End of snippet to copy/paste
    
    # Important! Review the file and check the values.
    cat azure-files-storage-class.yaml
    
    # Deploy the 
    kubectl apply -f azure-files-storage-class.yaml

    # Copy/paste the entire snippet BELOW (and then press ENTER) to create the data-protection-persistent-claim.yaml file
    cat << EOF > data-protection-persistent-claim.yaml
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
    # End of snippet to copy/paste
    
    # Important! Review the file and check the values.
    cat data-protection-persistent-claim.yaml
    
    # Deploy the 
    kubectl apply -f data-protection-persistent-claim.yaml

    # Copy/paste the entire snippet BELOW (and then press ENTER) to create the azure-pvc-roles.yaml file
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
    # End of snippet to copy/paste
    
    # Important! Review the file and check the values.
    cat azure-pvc-roles.yaml
    
    # Deploy the 
    kubectl apply -f azure-pvc-roles.yaml

    # Copy/paste the entire snippet BELOW (and then press ENTER) to create the msal-net-proxy.yaml file
    cat << EOF > msal-net-proxy.yaml
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
    # End of snippet to copy/paste
    
    # Important! Review the file and check the values
    cat msal-net-proxy.yaml
    
    # Deploy the 
    kubectl apply -f msal-net-proxy.yaml
    
    # Check to see if the services, deployment and pods are running and healthy
    kubectl get svc,deploy,pod

## Install Cert Manager

We will use Cert-Manager to create and install SSL Certs from Let'sEncrypt onto our K8S Cluster.  This is because AAD requires all endpoints be either [localhost](http://localhost) or HTTPS. 

Inspired by [https://docs.microsoft.com/en-us/azure/aks/ingress-tls](https://docs.microsoft.com/en-us/azure/aks/ingress-tls).

### Deploy Production Cert Manager

    # Set the secret name
    TLS_SECRET_NAME=ingress-tls-prod

    # Create the namespace 
    kubectl create namespace cert-manager
    
    # Deploy the jetpack CRD, role bindings
    kubectl apply -f https://raw.githubusercontent.com/jetstack/cert-manager/release-0.11/deploy/manifests/00-crds.yaml --validate=false
    
    # Add the Jetstack Helm repository
    helm repo add jetstack https://charts.jetstack.io
    
    # Update your local Helm chart repository cache
    helm repo update

    # Install the cert manager
    helm install --name cert-manager --namespace cert-manager --set ingressShim.defaultIssuerName=letsencrypt-prod --set ingressShim.defaultIssuerKind=ClusterIssuer jetstack/cert-manager --version v0.11.0
    
    # Make sure the cert-manager pods have started BEFORE proceeding. It can take 2-3 min for the cert-manager-webhook container to start up
    kubectl get pods -n cert-manager

    # Copy/paste the entire snippet BELOW (and then press ENTER) to create the cluster-issuer-prod.yaml file
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
    # End of snippet to copy/paste

    # Important! Review the file and check the values.
    cat cluster-issuer-prod.yaml
    
    # Deploy the issuer config to the cluster
    kubectl apply -f cluster-issuer-prod.yaml

## Deploy the Ingress Resources

    # Copy/paste the entire snippet BELOW (and then press ENTER) to create the hello-world-ingress.yaml file
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
    # End of snippet to copy/paste

    # Important! Review the file and check the values.
    cat hello-world-ingress.yaml
    
    # Deploy the ingress config to the cluster
    kubectl apply -f hello-world-ingress.yaml

## Verify Production Certificate works

After creating the ingress resource, this will initiate the Let's Encrypt certificate request process.   

    # Verify certificate - this may take a few minutes to show the cert as ready=true
    kubectl get certificate $TLS_SECRET_NAME

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
    kubectl delete secret ingress-tls-prod
    kubectl delete -f https://raw.githubusercontent.com/jetstack/cert-manager/release-0.11/deploy/manifests/00-crds.yaml
    kubectl delete ns cert-manager

# References

- [https://github.com/kubernetes-up-and-running/kuard](https://github.com/kubernetes-up-and-running/kuard)
- [https://docs.cert-manager.io/en/latest/getting-started/install/kubernetes.html](https://docs.cert-manager.io/en/latest/getting-started/install/kubernetes.html)
- [https://kubernetes.github.io/ingress-nginx/examples/auth/oauth-external-auth](https://kubernetes.github.io/ingress-nginx/examples/auth/oauth-external-auth)

