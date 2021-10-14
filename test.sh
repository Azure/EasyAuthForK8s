#!/bin/sh -x

# Initialize Variables for flags
A=''
C=''
R=''
E=''
D=''
L=''
I=''
P=''

while getopts "a:c:r:e:d:l:i:n:s:p:h" OPTION
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
        d)
			# echo "The value of -d is ${OPTARG} - EMAIL_DOMAIN"
            D=$OPTARG ;;
        l)
			# echo "The value of -l is ${OPTARG} - LOCATION"
            L=$OPTARG ;;
        i)
			# echo "The value of -i is ${OPTARG} - INPUTIMAGE"
            I=$OPTARG ;;
        p) 
            # echo "The value of -p is ${OPTARG} - SKIP_CLUSTER_CREATION"
            P=$OPTARG ;;
		h)
            # Change to how others show it like az
            echo "HELP: Here are the flags and their variables"
			echo "REQUIRED: -a is for AD_APP_NAME"
            echo "REQUIRED: -c is for CLUSTER_NAME *Note: Cluster Name must be unique*" 
            echo "REQUIRED: -r is for CLUSTER_RG"
            echo "REQUIRED: -e is for EMAIL"
            echo "REQUIRED: -d is for EMAIL_DOMAIN"
            echo "REQUIRED: -l is for LOCATION"
            echo "OPTOINAL: -i is for INPUTIMAGE"
            echo "OPTOINAL: -p is for SKIP_CLUSTER_CREATION"
			exit ;;
	esac
done

echo ""
echo "BEGIN @ $(date +"%T"): START OF END-TO-END TEST"
bash ./main.sh -a $A -c $C -r $R -e $E -d $D -l $L

APP_NAME="$A.$L.cloudapp.azure.com"
WEBPAGE=https://$APP_NAME
echo "Grabbed homepage: " $WEBPAGE
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