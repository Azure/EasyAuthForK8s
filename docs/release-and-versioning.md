# Release and Versioning Process

We use two types of environments: Development and Production. 

| Environment | Azure Credentials | Docker | Branch |
|--- | --- | ---| ---|
| Development | Individual Azure Subscription | Individual Docker Hub | feature |
| Production | Sandbox/Production Subscription | Public Docker Hub | master |

Each feature branch must use an individual developer's Docker Hub repo or a development Docker Hub repo specified by the EasyAuth team. This is done to avoid conflicts with other developers and conflicts with the versioned container releases. Once you complete a pull request with your changes, the Production workflow executes the validation process on master. 


| Trigger | Branch | Environment | EasyAuth Container Tag Created |
|--- | --- | ---| ---|
| pull request | feature/ | Development | latest |
| push to master | master/  | Production | master |
| release publish | master/ | Production | v* |


| Workflow | Trigger | EasyAuth Container Version Used in Testing | Behavior |
|--- | --- | ---| --- |
| E2E | pull request| latest | Builds Easy Auth .NET solution, builds Docker container and deploys Sample application |
| Prod | push to master| master | Builds Easy Auth .NET solution, builds and pushes Docker container to public Docker Hub, and deploys Sample Application |
| Release| release publish | v* | Builds Easy Auth .NET solution, builds and pushes Docker container to public Docker Hub with version tag, and deploys Sample Application. Cuts and tags release from most recent commit in master| 

