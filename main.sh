#!/bin/sh -x

echo ""
echo "BEGIN @ $(date +"%T"): START OF SCRIPT"

# Check Helm Installation
if ! [ -x "$(command -v helm)" ]; then
    echo "Please install Helm."
    exit
fi
# Check JQ Installation
if ! [ -x "$(command -v jq)" ]; then
    echo "Please install JQ. You can install by typing:"
    echo "sudo apt install jq"
    exit
fi
# Check Kubernetes Installation
if ! [ -x "$(command -v kubectl)" ]; then
    echo "Please install Kubernetes."
    exit
fi
# Check Azure CLI Installation
if ! [ -x "$(command -v az)" ]; then
    echo "Please install the Azure CLI."
    exit
fi

echo ""
# Show the subscription we will be deploying to.
echo "******We will be deploying to this subscription******"
az account show

echo ""
echo "BEGIN @ $(date +"%T"): Set variables..."

# Initialize Variables for flags
ITERATION=''
AD_APP_NAME=''
CLUSTER_NAME=''
CLUSTER_RG=''
EMAIL=''
EMAIL_DOMAIN=''
LOCATION=''
INPUTIMAGE=''
NAMESPACE=''
CLIENTID='' # The only thing I really need is CLIENT ID. With the client ID, we can skip creating the AAD App.
SKIP_CLUSTER_CREATION=''

while getopts "a:c:r:e:d:l:i:n:s:p:h" OPTION
do
	case $OPTION in
		a)
			# echo "The value of -a is ${OPTARG} - AD_APP_NAME"
            AD_APP_NAME=$OPTARG$ITERATION ;;
	    c)
			# echo "The value of -c is ${OPTARG} - CLUSTER_NAME"
            CLUSTER_NAME=$OPTARG$ITERATION ;;
        r)
			# echo "The value of -r is ${OPTARG} - CLUSTER_RG"
            CLUSTER_RG=$OPTARG$ITERATION ;;
        e)
			# echo "The value of -e is ${OPTARG} - EMAIL"
            EMAIL=$OPTARG ;;
        d)
			# echo "The value of -d is ${OPTARG} - EMAIL_DOMAIN"
            EMAIL_DOMAIN=$OPTARG ;;
        l)
			# echo "The value of -l is ${OPTARG} - LOCATION"
            LOCATION=$OPTARG ;;
        i)
			# echo "The value of -i is ${OPTARG} - INPUTIMAGE"
            INPUTIMAGE=$OPTARG ;;
        n)
	         # echo "The value of -n is ${OPTARG} - NAMESPACE"
            NAMESPACE=$OPTARG ;;
	    s)
	        # echo "The value of -s is ${OPTARG} - CLIENTID"
	        CLIENTID=$OPTARG ;;
        p) 
            # echo "The value of -p is ${OPTARG} - SKIP_CLUSTER_CREATION"
            SKIP_CLUSTER_CREATION=$OPTARG ;;
		h)
            # Change to how others show it like az
            echo "HELP: Here are the flags and their variables"
			echo "REQUIRED: -a is for AD_APP_NAME"
            echo "REQUIRED: -c is for CLUSTER_NAME"
            echo "REQUIRED: -r is for CLUSTER_RG"
            echo "REQUIRED: -e is for EMAIL"
            echo "REQUIRED: -d is for EMAIL_DOMAIN"
            echo "REQUIRED: -l is for LOCATION"
            echo "OPTOINAL: -i is for INPUTIMAGE"
            echo "OPTOINAL: -n is for NAMESPACE"
            echo "OPTOINAL: -s is for CLIENTID"
            echo "OPTOINAL: -p is for SKIP_CLUSTER_CREATION"
			exit ;;
	esac
done


# Force required flags.
if [ -z "$AD_APP_NAME" ] || [ -z "$CLUSTER_NAME" ] || [ -z "$CLUSTER_RG" ] || [ -z "$EMAIL" ] || [ -z "$EMAIL_DOMAIN" ] || [ -z "$LOCATION" ]; then
    echo "*****ERROR. Please enter all required flags.*****"
    exit
fi 

# If there is no flag set for SKIP_CLUSTER_CREATION, then create a random iteration.
if [ -z "$SKIP_CLUSTER_CREATION" ]; then
    ITERATION=$RANDOM
else
    ITERATION=''
fi

# ITERATION=34
# AD_APP_NAME="$1$ITERATION"
# CLUSTER_NAME=$2$ITERATION
# CLUSTER_RG=$3$ITERATION
# EMAIL=$4
# EMAIL_DOMAIN=$5
# LOCATION=$6
# NAMESPACE=$7
APP_HOSTNAME="$AD_APP_NAME.$LOCATION.cloudapp.azure.com"
HOMEPAGE=https://$APP_HOSTNAME
IDENTIFIER_URIS=$HOMEPAGE
REPLY_URLS=https://$APP_HOSTNAME/msal/signin-oidc
COOKIE_SECRET=$(python -c 'import os,base64; print(base64.b64encode(os.urandom(16)).decode("utf-8"))')
INGRESS_IP=0

echo "The value of -a is $AD_APP_NAME - AD_APP_NAME"
echo "The value of -c is $CLUSTER_NAME - CLUSTER_NAME"
echo "The value of -r is $CLUSTER_RG - CLUSTER_RG"
echo "The value of -e is $EMAIL - EMAIL"
echo "The value of -d is $EMAIL_DOMAIN - EMAIL_DOMAIN"
echo "The value of -l is $LOCATION - LOCATION"
echo "The value of -i is $INPUTIMAGE - INPUTIMAGE"
echo "The value of -n is $NAMESPACE - NAMESPACE"
echo "The value of -s is $CLIENTID - CLIENTID"
echo "The value of -p is $SKIP_CLUSTER_CREATION - SKIP_CLUSTER_CREATION"
echo "COOKIE_SECRET: " $COOKIE_SECRET
echo "COMPLETE @ $(date +"%T"): Setting variables"


echo "****BEGIN @ $(date +"%T"): Call AKS Cluster Creation script...****"
# If there is no flag set for SKIP_CLUSTER_CREATION, then create the AKS cluster.
if [ -z "$SKIP_CLUSTER_CREATION" ]; then
    . ./AutomationScripts/1-clusterCreation.sh
else
    echo "CLUSTER CREATION WAS SKIPPED!"
fi
echo "****COMPLETE @ $(date +"%T"): Done cluster creation script.****"

# Get the credentials outside of cluster creation script.
echo "BEGIN @ $(date +"%T"): Getting cluster creds..."
az aks get-credentials -g $CLUSTER_RG -n $CLUSTER_NAME
echo "COMPLETE @ $(date +"%T"): Getting cluster creds"


# Add Helm
helm repo add stable https://kubernetes-charts.storage.googleapis.com


echo "****BEGIN @ $(date +"%T"): Call Ingress Controller Creation script****"
. ./AutomationScripts/2-ingressCreation.sh
echo "****COMPLETE @ $(date +"%T"): Ingress controller created script****"

echo "BEGIN @ $(date +"%T"): Deploy sample app..."
# INPUTIMAGE=$7 
# If we have a parameter for an image install a custom image. If not, then we install kuard.
if [ -z "$INPUTIMAGE" ]; then
    echo "No image input, installing kuard."
    kubectl run kuard-pod --image=gcr.io/kuar-demo/kuard-amd64:1 --expose --port=8080
else
    echo "Your custom image $INPUTIMAGE installed"
    kubectl run kuard-pod --image=$INPUTIMAGE --expose --port=8080
fi
echo "COMPLETE @ $(date +"%T"): Deployed sample app"

echo "****BEGIN @ $(date +"%T"): Call ADD App Creation script****"
. ./AutomationScripts/3-registerAADApp.sh
echo "****COMPLETE @ $(date +"%T"): AAD App created script****"

echo "****BEGIN @ $(date +"%T"): Call Deploy MSAL Proxy script****"
. ./AutomationScripts/4-deployMSALProxy.sh
echo "****COMPLETE @ $(date +"%T"): Deployed MSAL Proxy script****"

echo "****BEGIN @ $(date +"%T"): Call Install Cert Manager script****"
. ./AutomationScripts/5-installCertManager.sh
echo "****COMPLETE @ $(date +"%T"): Installed Cert Manager script****"

echo "****BEGIN @ $(date +"%T"): Call Deploy New Ingress Resource script****"
. ./AutomationScripts/6-deployNewIngressResource.sh
echo "****COMPLETE @ $(date +"%T"): Deployed New Ingress Resource script****"


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
