#!/bin/sh -x

# Every file has one dot instead of two because we are calling main.sh, so we access the "current" directory which is where main.sh is located.

echo "BEGIN @ $(date +"%T"): Deploy MSAL Proxy..."

# kubectl apply -f msal-net-proxy.yaml

echo "BEGIN @ $(date +"%T"): Calling Helm..."
echo ""

helm install --set secret.azureadtenantid=$AZURE_TENANT_ID --set secret.azureadclientid=$CLIENT_ID --set secret.azureclientsecret=$CLIENT_SECRET msal-proxy ./charts/msal-proxy

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