#!/bin/sh -x

# Every file has one dot instead of two because we are calling main.sh, so we access the "current" directory which is where main.sh is located.

echo "BEGIN @ $(date +"%T"): Deploy MSAL Proxy..."

echo "BEGIN @ $(date +"%T"): Deploying secret..."
echo ""

kubectl create secret generic aad-secret \
  --from-literal=AZURE_TENANT_ID=$AZURE_TENANT_ID \
  --from-literal=CLIENT_ID=$CLIENT_ID \
  --from-literal=CLIENT_SECRET=$CLIENT_SECRET

echo ""
echo "COMPLETE @ $(date +"%T"): Deploying secret"

# kubectl apply -f msal-net-proxy.yaml

echo "BEGIN @ $(date +"%T"): Calling Helm..."
echo ""

#helm install msal-proxy ./charts/msal-proxy 
kubectl apply -f ./K8s-Config/msal-net-proxy.yaml
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