#!/bin/sh -x

echo "BEGIN @ $(date +"%T"): Deploy the Ingress Resources..."

kuard_ingress_yaml=$(<./K8s-Config/kuard-ingress.yaml)
#replace values
kuard_ingress_yaml=${kuard_ingress_yaml//"{{APP_HOSTNAME}}"/$APP_HOSTNAME}
kuard_ingress_yaml=${kuard_ingress_yaml//"{{TLS_SECRET_NAME}}"/$TLS_SECRET_NAME}
#write file - must use double quotes to preserve white space
cat <<< "$kuard_ingress_yaml" > ./K8s-Config/kuard-ingress.yaml

kubectl apply -f ./K8s-Config/kuard-ingress.yaml

echo "COMPLETE @ $(date +"%T"): Deploy the Ingress Resources"