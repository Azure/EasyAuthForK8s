#!/bin/sh -x

echo "BEGIN @ $(date +"%T"): Creating the resource group..."
az group create -n $CLUSTER_RG -l $LOCATION
echo "COMPLETE @ $(date +"%T"): Resource group created"

echo "BEGIN @ $(date +"%T"): Creating the cluster..."
az aks create -g $CLUSTER_RG -n $CLUSTER_NAME --generate-ssh-keys --node-count 1
echo "COMPLETE @ $(date +"%T"): Cluster created!"