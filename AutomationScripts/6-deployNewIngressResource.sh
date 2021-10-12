#!/bin/sh -x

echo "BEGIN @ $(date +"%T"): Deploy the Ingress Resources..."

sed "s/{{APP_HOSTNAME}}/$APP_HOSTNAME/g" ./K8s-Config/kuard-ingress.yaml > ./user/kuard-ingress.yaml
sed -i "s/{{TLS_SECRET_NAME}}/$TLS_SECRET_NAME/g" ./user/kuard-ingress.yaml
kubectl apply -f ./user/kuard-ingress.yaml

echo "COMPLETE @ $(date +"%T"): Deploy the Ingress Resources"