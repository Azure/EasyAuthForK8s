#!/bin/sh -x

echo "BEGIN @ $(date +"%T"): Deploy the Ingress Resources..."
cat << EOF > ../hello-world-ingress.yaml
apiVersion: extensions/v1beta1
kind: Ingress
metadata:
  name: hello-world-ingress
  annotations:
    nginx.ingress.kubernetes.io/auth-url: "https://\$host/msal/auth"
    nginx.ingress.kubernetes.io/auth-signin: "https://\$host/msal/index?rd=\$escaped_request_uri"
    nginx.ingress.kubernetes.io/auth-response-headers: "x-injected-aio,x-injected-name,x-injected-nameidentifier,x-injected-objectidentifier,x-injected-preferred_username,x-injected-tenantid,x-injected-uti"
    kubernetes.io/ingress.class: nginx
    kubernetes.io/tls-acme: "true"
    certmanager.k8s.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/rewrite-target: /\$1
spec:
  tls:
  - hosts:
    - $APP_HOSTNAME
    secretName: $TLS_SECRET_NAME
  rules:
  - host: $APP_HOSTNAME
    http:
      paths:
      - backend:
          serviceName: kuard-pod
          servicePort: 8080
        path: /(.*)
---
apiVersion: extensions/v1beta1
kind: Ingress
metadata:
  name: msal-net-proxy
spec:
  rules:
  - host: $APP_HOSTNAME
    http:
      paths:
      - backend:
          serviceName: msal-net-proxy
          servicePort: 80
        path: /msal
  tls:
  - hosts:
    - $APP_HOSTNAME
    secretName: $TLS_SECRET_NAME
EOF

cat hello-world-ingress.yaml

kubectl apply -f hello-world-ingress.yaml

echo "COMPLETE @ $(date +"%T"): Deploy the Ingress Resources"