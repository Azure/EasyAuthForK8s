#!/bin/sh -x

echo "BEGIN @ $(date +"%T"): Register AAD Application..."

CLIENT_ID=$(az ad app create --display-name $AD_APP_NAME --homepage $HOMEPAGE --reply-urls $REPLY_URLS --required-resource-accesses @./TemplateFiles/manifest.json --debug -o json | jq -r '.appId')
echo "CLIENT_ID: " $CLIENT_ID

# AAD core store is eventually consistent.  Usually we can retrieve the object on the first try after creation,
# but sometimes it takes a few seconds.
n=50
while [ -z "$OBJECT_ID" ]
do
  sleep 5
  echo "Polling status of AAD object creation for app...."
  OBJECT_ID=$(az ad app show --id $CLIENT_ID -o json | jq '.objectId' -r)
  if [ "$n" == "0" ]; then
    echo "ERROR. INFINITE LOOP in 3-registerAADApp.sh."
    exit 1
  fi
  n=$((n-1))
done

echo "OBJECT_ID: " $OBJECT_ID

# the default manifest template in the CLI command adds a permission we do not need
# Here we disable it, then delete it.  This also keeps us from getting dealing with
# unnessary consent prompts
az ad app update --id $OBJECT_ID --set oauth2Permissions[0].isEnabled=false
az ad app update --id $OBJECT_ID --set oauth2Permissions=[]

n=50
while [ -z "$CLIENT_SECRET" ]
do
  CLIENT_SECRET=$(az ad app credential reset --id $CLIENT_ID -o json | jq '.password' -r)
  echo "CLIENT_SECRET: " $CLIENT_SECRET
  if [ "$n" == "0" ]; then
    echo "ERROR. INFINITE LOOP in 3-registerAADApp.sh."
    exit 1
  fi
  n=$((n-1))
done

AZURE_TENANT_ID=$(az account show -o json | jq '.tenantId' -r)
echo "AZURE_TENANT_ID: " $AZURE_TENANT_ID

echo "COMPLETE @ $(date +"%T"): Register AAD Application"