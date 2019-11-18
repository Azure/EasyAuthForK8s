# Copy of K8 Auth Inject - Documentation

## Prerequisites

These are **critical dependencies to install prior** to running the commands below.

- [JQ](https://stedolan.github.io/jq/)
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest)
    - Important! Make sure you are running the LATEST version of the Azure CLI.
- Kubectl
- Helm

## Set Variables

Review these very carefully and modify.

    # Important! Set the name for your Azure AD App Registration. This will also be used for the Ingress DNS hostname.
    AD_APP_NAME="k-oauth2-proxy"
    
    # Set your AKS cluster name and resource group
    CLUSTER_NAME=k-cluster
    CLUSTER_RG=k-cluster-rg
    
    # Set the email address for the cluster certificate issuer
    EMAIL=kgates@microsoft.com
    
    # Set email domain for certificates
    EMAIL_DOMAIN=microsoft.com
    
    # Region to create resources
    LOCATION=southcentralus
    
    APP_HOSTNAME="$AD_APP_NAME.$LOCATION.cloudapp.azure.com"
    HOMEPAGE=https://$APP_HOSTNAME
    IDENTIFIER_URIS=$HOMEPAGE
    REPLY_URLS=https://$APP_HOSTNAME/oauth2/callback
    COOKIE_SECRET=$(python -c 'import os,base64; print(base64.b64encode(os.urandom(16)).decode("utf-8"))')
    echo $COOKIE_SECRET

## Create AKS Cluster

Note: It takes several minutes to create the AKS cluster. Complete these steps before proceeding to the next section.

    az group create -n $CLUSTER_RG -l $LOCATION
    az aks create -g $CLUSTER_RG -n $CLUSTER_NAME --vm-set-type VirtualMachineScaleSets
    az aks get-credentials -g $CLUSTER_RG -n $CLUSTER_NAME
    
    # Important! Wait for the steps above to complete before proceeding.

## Install Helm

    # Create the service account for tiller
    kubectl --namespace kube-system create serviceaccount tiller
    
    # Create cluster role binding
    kubectl create clusterrolebinding tiller-cluster-rule --clusterrole=cluster-admin --serviceaccount=kube-system:tiller
    
    # Helm init
    helm init --service-account tiller
    
    # Note: it make take a minute for the tiller service on the cluster to start

## Install NGINX Ingress

    # Install the ingress controller
    helm install stable/nginx-ingress --name nginx-ingress --namespace ingress-controllers --set rbac.create=true
    
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

    kubectl create deployment kuard --image=gcr.io/kuar-demo/kuard-amd64:1
    kubectl expose deployment kuard --port=8080

## Register AAD Application

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

    # Create the Azure AD App Registration and save the Client ID to a variable
    CLIENT_ID=$(az ad app create --display-name $AD_APP_NAME --homepage $HOMEPAGE --reply-urls $REPLY_URLS --required-resource-accesses @manifest.json -o json | jq -r '.appId')
    echo $CLIENT_ID
    
    # Get the object ID.  This is needed to remove the oauth2 permissions
    OBJECT_ID=$(az ad app show --id $CLIENT_ID -o json | jq '.objectId' -r)
    echo $OBJECT_ID
    az ad app update --id $OBJECT_ID --set oauth2Permissions[0].isEnabled=false
    az ad app update --id $OBJECT_ID --set oauth2Permissions=[]
    
    # The newly registered app does not have a password.  Use "az ad app credential reset" to add password and save to a variable.
    CLIENT_SECRET=$(az ad app credential reset --id $CLIENT_ID -o json | jq '.password' -r)
    echo $CLIENT_SECRET
    
    # Get your Azure AD tenant ID and save to variable
    AZURE_TENANT_ID=$(az account show -o json | jq '.tenantId' -r)
    echo $AZURE_TENANT_ID

## Deploy Oauth2_proxy

    # Create the deployment and service for Oauth2-proxy
    
    cat << EOF > oauth2_proxy.yaml
    apiVersion: extensions/v1beta1
    kind: Deployment
    metadata:
      labels:
        k8s-app: oauth2-proxy
      name: oauth2-proxy
    spec:
      replicas: 1
      selector:
        matchLabels:
          k8s-app: oauth2-proxy
      template:
        metadata:
          labels:
            k8s-app: oauth2-proxy
        spec:
          containers:
          - args:
            - --provider=azure
            - --email-domain=$EMAIL_DOMAIN
            - --http-address=0.0.0.0:4180
            - --azure-tenant=$AZURE_TENANT_ID
            env:
            - name: OAUTH2_PROXY_CLIENT_ID
              value: $CLIENT_ID
            - name: OAUTH2_PROXY_CLIENT_SECRET
              value: $CLIENT_SECRET
            - name: OAUTH2_PROXY_COOKIE_SECRET
              value: $COOKIE_SECRET
            image: quay.io/pusher/oauth2_proxy:latest
            imagePullPolicy: Always
            name: oauth2-proxy
            ports:
            - containerPort: 4180
              protocol: TCP
    ---
    apiVersion: v1
    kind: Service
    metadata:
      labels:
        k8s-app: oauth2-proxy
      name: oauth2-proxy
    spec:
      ports:
      - name: http
        port: 4180
        protocol: TCP
        targetPort: 4180
      selector:
        k8s-app: oauth2-proxy
    EOF
    
    kubectl apply -f oauth2_proxy.yaml
    
    # If you get the error below, you need to wait ~1 minute for AAD to propogate all data
    # "This can happen if the application has not been installed by the administrator of the tenant or consented to by any user in the tenant."
    
    
    # Important! Review the file and check the values
    cat oauth2_proxy.yaml
    
    # Deploy the oauth2 proxy to the cluster
    kubectl apply -f oauth2_proxy.yaml
    
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

    # Copy/paste the entire snippet BELOW (and then press ENTER) to create the ingress-prod.yaml file
    cat << EOF > ingress-prod.yaml
    apiVersion: extensions/v1beta1
    kind: Ingress
    metadata:
      name: nginx
      annotations:
        kubernetes.io/ingress.class: nginx
        kubernetes.io/tls-acme: "true"
        nginx.ingress.kubernetes.io/auth-url: "https://\$host/oauth2/auth"
        nginx.ingress.kubernetes.io/auth-signin: "https://\$host/oauth2/start?rd=\$escaped_request_uri"
    spec:
      tls:
      - hosts:
        - $APP_HOSTNAME
        secretName: $TLS_SECRET_NAME
      rules:
      - host: $APP_HOSTNAME
        http:
          paths:
          - path: /
            backend:
              serviceName: kuard
              servicePort: 8080
    ---
    apiVersion: extensions/v1beta1
    kind: Ingress
    metadata:
      name: oauth2-proxy
    spec:
      rules:
      - host: $APP_HOSTNAME
        http:
          paths:
          - backend:
              serviceName: oauth2-proxy
              servicePort: 4180
            path: /oauth2
      tls:
      - hosts:
        - $APP_HOSTNAME
        secretName: $TLS_SECRET_NAME
    EOF
    # End of snippet to copy/paste

    # Important! Review the file and check the values.
    cat ingress-prod.yaml
    
    # Deploy the ingress config to the cluster
    kubectl apply -f ingress-prod.yaml

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

- [https://kubernetes.github.io/ingress-nginx/examples/auth/oauth-external-auth](https://kubernetes.github.io/ingress-nginx/examples/auth/oauth-external-auth)
- [https://docs.cert-manager.io/en/latest/getting-started/install/kubernetes.html](https://docs.cert-manager.io/en/latest/getting-started/install/kubernetes.html)
