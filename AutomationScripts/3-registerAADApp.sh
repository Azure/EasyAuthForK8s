#!/bin/sh -x

echo "BEGIN @ $(date +"%T"): Register AAD Application..."
cat << EOF > manifest.json
[
   {
      "resourceAccess" : [
         {
            "id" : "e1fe6dd8-ba31-4d61-89e7-88639da4683d",
            "type" : "Scope"
         }
      ],
      "resourceAppId" : "00000003-0000-0000-c000-000000000000"
   }
]
EOF

CLIENT_ID=$(az ad app create --display-name $AD_APP_NAME --homepage $HOMEPAGE --reply-urls $REPLY_URLS --required-resource-accesses @manifest.json -o json | jq -r '.appId')
echo "CLIENT_ID: " $CLIENT_ID

OBJECT_ID=$(az ad app show --id $CLIENT_ID -o json | jq '.objectId' -r)
echo "OBJECT_ID: " $OBJECT_ID
az ad app update --id $OBJECT_ID --set oauth2Permissions[0].isEnabled=false
az ad app update --id $OBJECT_ID --set oauth2Permissions=[]

CLIENT_SECRET=$(az ad app credential reset --id $CLIENT_ID -o json | jq '.password' -r)
echo "CLIENT_SECRET: " $CLIENT_SECRET

AZURE_TENANT_ID=$(az account show -o json | jq '.tenantId' -r)
echo "AZURE_TENANT_ID: " $AZURE_TENANT_ID

echo "COMPLETE @ $(date +"%T"): Register AAD Application"