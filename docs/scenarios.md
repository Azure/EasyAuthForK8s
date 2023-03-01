# Advanced Scenarios

## Multi-tenant apps
For applications that need to support multiple Azure AD tenants independently, you can configure and deploy multiple EasyAuth pods.  As long as you can distinguish different tenants with ingress rules, you will be able to route auth requests to the correct pod.

For example, let's say you have an application with the url "https://mysharedapp.constoso.com/".  This app is a multitenant evironment, where the base url path identifies the tenant within the application ("https://mysharedapp.constoso.com/fabrikam).  Configure the helm chart values of each EasyAuth pod with a unique `basePath`, so that the ingress rules can route auth requests to the correct pod.  Assuming we use "fabrikam" as the basePath for our sample tenant, your ingress configuration would look something like:

```
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: easyauth-fabrikam-tenant
  annotations:
    nginx.ingress.kubernetes.io/auth-url: "https://$host/fabrikam/auth"
    nginx.ingress.kubernetes.io/auth-signin: "https://$host/fabrikam/login"
    nginx.ingress.kubernetes.io/auth-response-headers: "x-injected-userinfo,x-injected-name,x-injected-oid,x-injected-preferred-username,x-injected-sub,x-injected-tid,x-injected-email,x-injected-groups,x-injected-scp,x-injected-roles,x-injected-graph"
    cert-manager.io/cluster-issuer: {{your-cert-manager}}

spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - {{APP_HOSTNAME}}
    secretName: {{TLS_SECRET_NAME}}
  rules:
  - host: {{APP_HOSTNAME}}
    http:
      paths:
      - path: /fabrikam
        pathType: Prefix
        backend:
          service:
            name: mysharedapp-pod
            port:
              number: 80
```


You will also need to update your Azure AD App Registration (or create a new one) to include the OIDC reply url for the fabrikam EasyAuth pod.  The url will be in the form of `https://host/{{baseUrl}}/{{azureAd.callbackPath}}`, which in this case would be "https://mysharedapp.constoso.com/fabrikam//signin-oidc".  See [Add a redirect URI](https://learn.microsoft.com/en-us/azure/active-directory/develop/quickstart-register-app#add-a-redirect-uri) for more information.

Finally, you will need to update the helm chart values to reflect fabrikam's Azure AD tenant settings.  At a miminum, you'll need to set `azureAd.tenantId` to the GUID Id of fabrikam's Azure AD tenant, as well as the `azureAd.domain` value (not required, but provides the best user experience).  If you are sharing the same App Registration among EasyAuth pods, the `clientId` value will be the same.  In all cases where the App Registration is configured in a tenant that is different than the `azureAd.tenantId` value, you'll need to ensure that the App Registraion is [Multitenant](https://learn.microsoft.com/en-us/azure/active-directory/develop/single-and-multi-tenant-apps).