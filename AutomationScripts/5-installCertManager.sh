#!/bin/sh -x

echo "BEGIN @ $(date +"%T"): Install Cert Manager..."
TLS_SECRET_NAME=$APP_HOSTNAME-tls

kubectl create namespace cert-manager

# kubectl apply -f https://raw.githubusercontent.com/jetstack/cert-manager/release-0.11/deploy/manifests/00-crds.yaml --validate=false

helm repo add jetstack https://charts.jetstack.io

helm repo update

# helm install cert-manager --namespace cert-manager --set ingressShim.defaultIssuerName=letsencrypt-prod --set ingressShim.defaultIssuerKind=ClusterIssuer jetstack/cert-manager --version v0.11.0

helm install \
  cert-manager jetstack/cert-manager \
  --namespace cert-manager \
  --version v1.3.1 \
  --set installCRDs=true \
  --set ingressShim.defaultIssuerName=letsencrypt-prod \
  --set ingressShim.defaultIssuerKind=ClusterIssuer

kubectl get pods -n cert-manager

echo "Make sure the cert-manager pods have started BEFORE proceeding."

INPUT_STRING=no
while [ "$INPUT_STRING" != "yes" ]
do
  echo ""
  kubectl get pods -n cert-manager  
  echo ""
  echo "Did the cert-manager pods start OK? Type 'yes' or press enter to try again..."
  read INPUT_STRING
done

cluster_issuer_prod_yaml=$(<./K8s-Config/cluster-issuer-prod.yaml)
#replace values
cluster_issuer_prod_yaml=${cluster_issuer_prod_yaml//"{{EMAIL}}"/$EMAIL}
#write file - must use double quotes to preserve white space
echo "$cluster_issuer_prod_yaml" > ./K8s-Config/cluster-issuer-prod.yaml

kubectl apply -f ./K8s-Config/cluster-issuer-prod.yaml

echo "COMPLETE @ $(date +"%T"): Install Cert Manager"