#!/bin/sh -x

echo ""
echo "BEGIN @ $(date +"%T"): START OF END-TO-END TEST"
. ./main.sh -a dk-msal-test51 -c dk-msal-test51 -r dk-msal-test51 -e dakondra@microsoft.com -d microsoft.com -l eastus

echo "Grabbed homepage: " $HOMEPAGE
RESPONSE_CODE=$(curl -s -o /dev/null -w "%{http_code}" $HOMEPAGE)
echo "response code: " $RESPONSE_CODE

if [ "$RESPONSE_CODE" != "302" ]; then
    echo "ERROR! Project is not successfully deployed in K8s."
    exit 1
else
    echo "Project is successfully deployed in K8s!"
fi

echo "COMPLETE @ $(date +"%T"): END-TO-END TEST COMPLETED."
echo ""