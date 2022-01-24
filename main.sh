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
echo "BEGIN @ $(date +"%T"): Set variables..."

# Initialize Variables for flags
AD_APP_NAME=''
CLUSTER_NAME=''
CLUSTER_RG=''
EMAIL=''
LOCATION=''
INPUTIMAGE=''
ALT_TENANT_ID=''
SKIP_CLUSTER_CREATION=''
E2E_TEST_FLAG=''
SP=''
SP_SECRET=''

while getopts "a:c:r:e:l:i:t:s:z:pgh" OPTION
do
	case $OPTION in
		a)
            AD_APP_NAME=$OPTARG ;;
	    c)
            CLUSTER_NAME=$OPTARG ;;
        r)
            CLUSTER_RG=$OPTARG ;;
        e)
            EMAIL=$OPTARG ;;
        l)
            LOCATION=$OPTARG ;;
        i)
            INPUTIMAGE=$OPTARG ;;
        t)
            ALT_TENANT_ID=$OPTARG ;;
        s)
            SP=$OPTARG ;;
        z)
            SP_SECRET=$OPTARG ;;
        p) 
            SKIP_CLUSTER_CREATION="True" ;;
        g) 
            E2E_TEST_FLAG="True" ;;  
		h)
            # Change to how others show it like az
            echo "HELP: Here are the flags and their variables"
			echo "REQUIRED: -a is for AD_APP_NAME"
            echo "REQUIRED: -c is for CLUSTER_NAME *Note: Cluster Name must be unique*" 
            echo "REQUIRED: -r is for CLUSTER_RG"
            echo "REQUIRED: -e is for EMAIL"
            echo "REQUIRED: -l is for LOCATION"
            echo "OPTOINAL: -i is for INPUTIMAGE"
            echo "OPTOINAL: -t is for ALT_TENANT_ID"
            echo "OPTOINAL: -s is for SERVICE_PRICIPAL"
            echo "OPTOINAL: -z is for SP_SECRET"
            echo "OPTOINAL: -p is for SKIP_CLUSTER_CREATION"
			exit ;;
	esac
done

# Force required flags.
if [ -z "$AD_APP_NAME" ] || [ -z "$CLUSTER_NAME" ] || [ -z "$CLUSTER_RG" ] || [ -z "$EMAIL" ] || [ -z "$LOCATION" ]; then
    echo "*****ERROR. Please enter all required flags.*****"
    exit
fi 

echo ""
# Show the subscription we will be deploying to.
if [ -z "$E2E_TEST_FLAG" ]; then
    echo "******We will be deploying to this subscription******"
    az account show
fi

APP_HOSTNAME="$AD_APP_NAME.$LOCATION.cloudapp.azure.com"
HOMEPAGE=https://$APP_HOSTNAME
IDENTIFIER_URIS=$HOMEPAGE
REPLY_URLS=https://$APP_HOSTNAME/easyauth/signin-oidc

echo "The value of -a is $AD_APP_NAME - AD_APP_NAME"
echo "The value of -c is $CLUSTER_NAME - CLUSTER_NAME"
echo "The value of -r is $CLUSTER_RG - CLUSTER_RG"
echo "The value of -e is $EMAIL - EMAIL"
echo "The value of -l is $LOCATION - LOCATION"
echo "The value of -i is $INPUTIMAGE - INPUTIMAGE"
echo "The value of -t is $ALT_TENANT_ID - ALT_TENANT_ID"
echo "The value of -s is $SP - SERVICE_PRICIPAL"
echo "The value of -z is $SP_SECRET - SP_SECRET"
echo "The value of -p is $SKIP_CLUSTER_CREATION - SKIP_CLUSTER_CREATION"
echo "The value of -g is $E2E_TEST_FLAG - E2E_TEST_FLAG"
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
helm repo add stable https://charts.helm.sh/stable


echo "****BEGIN @ $(date +"%T"): Call Ingress Controller Creation script****"
. ./AutomationScripts/2-ingressCreation.sh
echo "****COMPLETE @ $(date +"%T"): Ingress controller created script****"

echo "****BEGIN @ $(date +"%T"): Call ADD App Creation script****"
. ./AutomationScripts/3-registerAADApp.sh
echo "****COMPLETE @ $(date +"%T"): AAD App created script****"

echo "****BEGIN @ $(date +"%T"): Call Install Cert Manager script****"
. ./AutomationScripts/4-installCertManager.sh
echo "****COMPLETE @ $(date +"%T"): Installed Cert Manager script****"

echo "****BEGIN @ $(date +"%T"): Call Deploy MSAL Proxy script****"
. ./AutomationScripts/5-deployMSALProxy.sh
echo "****COMPLETE @ $(date +"%T"): Deployed MSAL Proxy script****"

echo "BEGIN @ $(date +"%T"): Deploy sample app..."
# If we have a parameter for an image install a custom image. If not, then we install kuard.
if [ -z "$INPUTIMAGE" ]; then
    echo "No image input, installing sample."
    kubectl run easyauth-sample-pod --image=docker.io/dakondra/eak-test-container:latest --expose --port=80
else
    echo "Your custom image $INPUTIMAGE installed"
    kubectl run custom-pod --image=$INPUTIMAGE --expose --port=80
fi
echo "COMPLETE @ $(date +"%T"): Deployed sample app"

echo "****BEGIN @ $(date +"%T"): Call Deploy New Ingress Resource script****"
. ./AutomationScripts/6-deployNewIngressResource.sh
echo "****COMPLETE @ $(date +"%T"): Deployed New Ingress Resource script****"

echo "BEGIN @ $(date +"%T"): Verify Production Certificate works..."
INPUT_STATUS=false
n=50
while [[ "$INPUT_STATUS" != "True" || "$INPUT_TYPE" != "Ready" ]]
do
  echo ""
  kubectl get certificate $TLS_SECRET_NAME
  INPUT_STATUS=$(kubectl get certificate $TLS_SECRET_NAME -o=jsonpath='{.status.conditions[0].status}')
  INPUT_TYPE=$(kubectl get certificate $TLS_SECRET_NAME -o=jsonpath='{.status.conditions[0].type}')
  echo "status: " $INPUT_STATUS
  echo "type: " $INPUT_TYPE
  sleep 5
  if [ "$n" == "0" ]; then
    echo "ERROR. INFINITE LOOP in main.sh when calling kubectl get certificate."
    exit 1
  fi
  n=$((n-1))
  echo ""
done
echo "COMPLETE @ $(date +"%T"): Verify Production Certificate works"
echo "END OF SCRIPT"
echo ""
echo "NOTE: Please wait 5 minutes for Azure AD to complete setup before opening the homepage!"
echo ""
echo "Visit the app in the browser. Good luck! " $HOMEPAGE
echo ""
echo ""