[![Actions Status](https://github.com/Azure/EasyAuthForK8s/workflows/Docker%20Build%20and%20Push/badge.svg)](https://github.com/Azure/EasyAuthForK8s/actions)

# EasyAuth for Kubernetes

EasyAuth for Kubernetes is a simple Identity and Access Management module that allows you to protect applications in a kubernetes cluster without having to modify application source code.

Similar to the [security features](https://docs.microsoft.com/en-us/azure/app-service/overview-authentication-authorization) of Azure App Service, EasyAuth for Kubernetes is designed to do four basic functions:
* Authenticate callers via Azure Active Directory
* Validate and refresh tokens
* Manage authenticated sessions
* Inject basic information about the user into the request received by your application

EasyAuth uses the [Microsoft Authentication Libary (MSAL)](https://docs.microsoft.com/en-us/azure/active-directory/develop/msal-overview)  and Azure AD v2 endpoints, which allows you leverage all features of the [Microsoft Identity Platform](https://docs.microsoft.com/en-us/azure/active-directory/develop/v2-overview).

A few of these features include:
* Authenticating employees or business partners
* Azure AD B2C custom policies for complex identity flows
* MFA and conditional access
* Role-based access control
* Multitenant applications

## Concepts
EasyAuth for Kubernetes integrates with your cluster's [ingress controller](https://kubernetes.io/docs/concepts/services-networking/ingress-controllers/).  When a request is received, the EasyAuth service validates the user's session.  If the caller isn't authenticated yet, the service will route the caller to the appropriate Azure AD tenant to sign in.  The service then starts a managed session for the user and adds a cookie or bearer token to the response that will be used to authenticate the caller on future requests.

> Note: The authentication flow supports single sign on, so the user will not be prompted for credentials if they are already signed via the Azure AD tenant.  Also, user sessions are by default short-lived (60 minutes), so EasyAuth will request a new token and refresh the cookie as needed to maintain the session.

![Basic Flow](docs/media/basic-flow.jpg)

The diagram above conveys the basic interaction between the components for a user accessing a web application. For simplicity, this shows a single protected application and a single EasyAuth service.  You can, however, configure as many EasyAuth services as you would like within your cluster.  Each service can protect one or more applications, and have different Azure AD tenant configurations.   

> For example, you could have one set of applications accessible only by internal employees and another set of applications for customers or external users all running in the same cluster and using the same ingress controller.

## Quickstart using Azure Cloud Shell
Try it out by setting up a new AKS cluster with a sample application that uses EasyAuth.  Launch [Cloud Shell](https://shell.azure.com/bash), then run the following bash commands:
```
git clone https://github.com/Azure/EasyAuthForK8s.git

# hint - run bash main.sh -h for parameter help
# Note: Cluster name (-c) must be unique
bash ./EasyAuthForK8s/main.sh -a easy-auth-demo -c {insert-unique-hostname} -r easy-auth -e email@contoso.com -l eastus
```

## Documentation
* [Setup Instructions](docs/setup-instructions.md) - step-by-step guide to building a cluster, configuring the ingress controller, and deploying an application protected by EasyAuth
* [Detailed Architecture](docs/detailed-architecture.md) - an in-depth guide to how EasyAuth works
* [Calling a Protected API](docs/protecting-an-api.md) - how to authenticate a client application and call an API protected by EasyAuth
* [Http Headers](docs/headers.md) - Get information about the user that is accessing your application


# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
