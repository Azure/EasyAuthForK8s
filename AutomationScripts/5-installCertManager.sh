#!/bin/sh -x

echo "BEGIN @ $(date +"%T"): Install Cert Manager..."
TLS_SECRET_NAME=ingress-tls-prod

kubectl create namespace cert-manager

# kubectl apply -f https://raw.githubusercontent.com/jetstack/cert-manager/release-0.11/deploy/manifests/00-crds.yaml --validate=false

helm repo add jetstack https://charts.jetstack.io

helm repo update

# helm install cert-manager --namespace cert-manager --set ingressShim.defaultIssuerName=letsencrypt-prod --set ingressShim.defaultIssuerKind=ClusterIssuer jetstack/cert-manager --version v0.11.0

helm install \
  cert-manager jetstack/cert-manager \
  --namespace cert-manager \
  --create-namespace \
  --version v1.3.1 \
  --set installCRDs=true \
  --set ingressShim.defaultIssuerName=letsencrypt-prod \
  --set ingressShim.defaultIssuerKind=ClusterIssuer

kubectl get pods -n cert-manager

cat << EOF > ./K8s-Config/cluster-issuer-prod.yaml
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

cat ./K8s-Config/cluster-issuer-prod.yaml

INPUT_STRING=no
while [ "$INPUT_STRING" != "yes" ]
do
  echo ""
  kubectl get pods -n cert-manager  
  echo ""
  echo "Did the cert-manager pods start OK? Type 'yes' or press enter to try again..."
  read INPUT_STRING
done

kubectl apply -f ./K8s-Config/cluster-issuer-prod.yaml

echo "COMPLETE @ $(date +"%T"): Install Cert Manager"