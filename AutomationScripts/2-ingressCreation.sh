#!/bin/sh -x

echo "BEGIN @ $(date +"%T"): Installing the ingress controller..."
kubectl create ns ingress-controllers
helm install stable/nginx-ingress --name nginx-ingress --namespace=ingress-controllers --set rbac.create=true

INGRESS_IP=$(kubectl get services/nginx-ingress-controller -n ingress-controllers -o jsonpath="{.status.loadBalancer.ingress[0].ip}")

while [ "$INGRESS_IP" = "" ]
do
  echo "UPDATE @ $(date +"%T"): Checking for INGRESS_IP from Azure..."
  INGRESS_IP=$(kubectl get services/nginx-ingress-controller -n ingress-controllers -o jsonpath="{.status.loadBalancer.ingress[0].ip}")
  echo "UPDATE @ $(date +"%T"): Sleeping for 5 seconds..."
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
