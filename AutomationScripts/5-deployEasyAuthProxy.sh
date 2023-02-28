#!/bin/sh -x

# Every file has one dot instead of two because we are calling main.sh, so we access the "current" directory which is where main.sh is located.

echo "BEGIN @ $(date +"%T"): Deploy EasyAuth Proxy..."

echo "BEGIN @ $(date +"%T"): Calling Helm..."
echo ""
if [-z "$PROXY_VERSION"]
  helm install --set azureAd.tenantId=$AZURE_TENANT_ID --set azureAd.clientId=$CLIENT_ID --set secret.name=easyauth-proxy-$AD_APP_NAME-secret --set secret.azureclientsecret=$CLIENT_SECRET --set appHostName=$APP_HOSTNAME --set tlsSecretName=$TLS_SECRET_NAME --set image.tag=$PROXY_VERSION easyauth-proxy-$AD_APP_NAME ./charts/easyauth-proxy
else
  helm install --set azureAd.tenantId=$AZURE_TENANT_ID --set azureAd.clientId=$CLIENT_ID --set secret.name=easyauth-proxy-$AD_APP_NAME-secret --set secret.azureclientsecret=$CLIENT_SECRET --set appHostName=$APP_HOSTNAME --set tlsSecretName=$TLS_SECRET_NAME easyauth-proxy-$AD_APP_NAME ./charts/easyauth-proxy
fi 

echo ""
echo "COMPLETE @ $(date +"%T"): Calling Helm"

kubectl get svc,deploy,pod
  
INPUT_STRING=false
n=50
while [ "$INPUT_STRING" != "true" ]
do
  echo ""
  kubectl get svc,deploy,pod
  echo ""
  INPUT_STRING=$(kubectl get svc,deploy,pod -o=jsonpath='{.items[2].status.containerStatuses[0].ready}')
  sleep 10
  if [ "$n" == "0" ]; then
    echo "ERROR. INFINITE LOOP in 4-EasyAuthProxy.sh."
    exit 1
  fi
  n=$((n-1))
done

echo "COMPLETE @ $(date +"%T"): Deploy EasyAuth Proxy"