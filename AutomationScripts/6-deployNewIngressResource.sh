#!/bin/sh -x

echo "BEGIN @ $(date +"%T"): Deploy the Ingress Resources..."
cat << EOF > ./K8s-Config/kuard-ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: kuard
  annotations:
    nginx.ingress.kubernetes.io/auth-url: "https://\$host/msal/auth"
    nginx.ingress.kubernetes.io/auth-signin: "https://\$host/msal/index?rd=\$escaped_request_uri"
    nginx.ingress.kubernetes.io/auth-response-headers: "x-injected-aio,x-injected-name,x-injected-nameidentifier,x-injected-objectidentifier,x-injected-preferred_username,x-injected-tenantid,x-injected-uti"
    kubernetes.io/tls-acme: "true"
    certmanager.k8s.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/rewrite-target: /\$1
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - $APP_HOSTNAME
    secretName: $TLS_SECRET_NAME
  rules:
  - host: $APP_HOSTNAME
    http:
      paths:
      - backend:
          service:
            name: kuard-pod
            port:
              number: 8080
        path: /(.*)
        pathType: ImplementationSpecific
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: msal-proxy
spec:
  rules:
  - host: $APP_HOSTNAME
    http:
      paths:
      - backend:
          service:
            name: msal-proxy
            port: 
              number: 80
        path: /msal
        pathType: ImplementationSpecific
  tls:
  - hosts:
    - $APP_HOSTNAME
    secretName: $TLS_SECRET_NAME
EOF

cat ./K8s-Config/kuard-ingress.yaml

kubectl apply -f ./K8s-Config/kuard-ingress.yaml

echo "COMPLETE @ $(date +"%T"): Deploy the Ingress Resources"