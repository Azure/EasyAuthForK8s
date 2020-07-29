# Secure Kubernetes Dashboard

These steps assume that you have completed setup-instructions.md and now want to secure the K8S Dashboard.

## Install K8S Dashboard

```
helm repo add kubernetes-dashboard https://kubernetes.github.io/dashboard/
helm update
# protocolHttp=true is set because otherwise, the dashboard will deny any HTTP request.
# Since the Ingress Controller is terminating SSL, the request from the Ingress controller to the dashboard is HTTP.
helm upgrade -i kubernetes-dashboard kubernetes-dashboard/kubernetes-dashboard \
    --set ingress.enabled=false \
    --set protocolHttp=true \
    --set serviceAccount.create=true \
    --set=service.externalPort=80
```

## Deploy the Ingress Resources

Each Ingress resource to authenticate requires a FQDN and SSL.  For the setup-instructions.md example, we added a hostname to the Public IP attached to the Ingress Controller and used that for our ingress host.  For this example, we wiill remove those existing rules and replace them with the Dashboard

```
# Delete the existing ingress rules
kubectl delete ingress kuard msal-proxy

# Ensure the required envirionment variables are set:
echo $APP_HOSTNAME
echo $TLS_SECRET_NAME

# Create two new ingress rules. (Dashboard + MSAL)
cat << EOF > k8s-dashboard-ingress.yaml
apiVersion: extensions/v1beta1
kind: Ingress
metadata:
  name: dashboard
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
          serviceName: kubernetes-dashboard
          servicePort: 80
        path: /(.*)
---
apiVersion: extensions/v1beta1
kind: Ingress
metadata:
  name: msal-proxy
spec:
  rules:
  - host: $APP_HOSTNAME
    http:
      paths:
      - backend:
          serviceName: msal-proxy
          servicePort: 80
        path: /msal
  tls:
  - hosts:
    - $APP_HOSTNAME
    secretName: $TLS_SECRET_NAME
EOF

kubectl apply -f k8s-dashboard-ingress.yaml 
```

## Validate the Dashboard

In the browser open $APP_HOSTNAME.

By default, the dashboard will not have permissions to view.  You will need to update the role bindings for the app.

*DO NOT DO THIS IN PRODUCTION*

```
# You can update the cluster role binding for the dashboard by binding the `kubernetes-dashboard` service account with the `cluster-admin` role.  This is a huge security risk.
kubectl create clusterrolebinding kubernetes-dashboard-cluster --clusterrole=cluster-admin --serviceaccount=default:kubernetes-dashboard
```