#!/bin/sh -x

# Initialize Variables for flags
A=''
C=''
R=''
E=''
L=''
I=''
T=''
P=''
S=''
Z=''

while getopts "a:c:r:e:l:i:t:s:z:p" OPTION
do
	case $OPTION in
		a)
			# echo "The value of -a is ${OPTARG} - AD_APP_NAME"
            A=$OPTARG ;;
	    c)
			# echo "The value of -c is ${OPTARG} - CLUSTER_NAME"
            C=$OPTARG ;;
        r)
			# echo "The value of -r is ${OPTARG} - CLUSTER_RG"
            R=$OPTARG ;;
        e)
			# echo "The value of -e is ${OPTARG} - EMAIL"
            E=$OPTARG ;;
        l)
			# echo "The value of -l is ${OPTARG} - LOCATION"
            L=$OPTARG ;;
        i)
			# echo "The value of -i is ${OPTARG} - INPUTIMAGE"
            I=$OPTARG ;;
        t)
			# echo "The value of -t is ${OPTARG} - ALT_TENANT_ID"
            T=$OPTARG ;;  
        s)
            # echo "The value of -s is ${OPTARG} - SP"
            S=$OPTARG ;;
        z)
            # echo "The value of -z is ${OPTARG} - SP_SECRET"
            Z=$OPTARG ;;
        p) 
            # echo "The value of -p is ${OPTARG} - SKIP_CLUSTER_CREATION"
            P=$OPTARG ;;
	esac
done

echo ""
echo "BEGIN @ $(date +"%T"): START OF END-TO-END TEST"
bash ./main.sh -a $A -c $C -r $R -e $E -l $L 
# -t $T -s $S -z $Z -g

APP_NAME="$A.$L.cloudapp.azure.com"
WEBPAGE=https://$APP_NAME
echo "Grabbed homepage: " $WEBPAGE ". SLEEPING for 60 seconds..."
sleep 60
RESPONSE_CODE=$(curl -s -o /dev/null -w "%{http_code}" $WEBPAGE)
echo "response code: " $RESPONSE_CODE

if [ "$RESPONSE_CODE" != "302" ]; then
    echo "ERROR! Project is not successfully deployed in K8s."
    exit 1
else
    echo "Project is successfully deployed in K8s!"
fi

echo "COMPLETE @ $(date +"%T"): END-TO-END TEST COMPLETED."
echo ""