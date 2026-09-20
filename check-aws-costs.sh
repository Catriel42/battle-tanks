#!/bin/bash

echo "========================================================"
echo "🔍 1. FACTURACIÓN Y COSTOS DEL ÚLTIMO MES (Cost Explorer)"
echo "========================================================"
aws ce get-cost-and-usage \
  --time-period Start=$(date -d "30 days ago" +%Y-%m-%d 2>/dev/null || date -v-30d +%Y-%m-%d),End=$(date +%Y-%m-%d) \
  --granularity MONTHLY \
  --metrics "UnblendedCost" \
  --group-by Type=DIMENSION,Key=SERVICE \
  --query "ResultsByTime[*].Groups[?Metrics.UnblendedCost.Amount > '0.01'].{Servicio:Keys[0], CostoUSD:Metrics.UnblendedCost.Amount}" \
  --output table

echo ""
echo "========================================================"
echo "🖥️  2. INSTANCIAS EC2 ENCENDIDAS"
echo "========================================================"
aws ec2 describe-instances \
  --filters "Name=instance-state-name,Values=running,pending" \
  --query "Reservations[*].Instances[*].{ID:InstanceId, Tipo:InstanceType, IP_Publica:PublicIpAddress, Estado:State.Name}" \
  --output table

echo ""
echo "========================================================"
echo "💽 3. DISCOS EBS HUÉRFANOS (Sin máquina asociada - COBRAN)"
echo "========================================================"
aws ec2 describe-volumes \
  --filters Name=status,Values=available \
  --query "Volumes[*].{ID:VolumeId, SizeGB:Size, Tipo:VolumeType, Creado:CreateTime}" \
  --output table

echo ""
echo "========================================================"
echo "🌐 4. ELASTIC IPs OCIOSAS (Sin asociar - COBRAN MULTA)"
echo "========================================================"
aws ec2 describe-addresses \
  --query "Addresses[?InstanceId==null].{IP:PublicIp, AllocationId:AllocationId}" \
  --output table

echo ""
echo "========================================================"
echo "🗄️  5. BASES DE DATOS RDS / AURORA ACTIVAS"
echo "========================================================"
aws rds describe-db-instances \
  --query "DBInstances[*].{ID:DBInstanceIdentifier, Engine:Engine, Estado:DBInstanceStatus, Clase:DBInstanceClass}" \
  --output table

aws rds describe-db-clusters \
  --query "DBClusters[*].{Cluster:DBClusterIdentifier, Engine:Engine, Estado:Status}" \
  --output table

echo ""
echo "========================================================"
echo "📸 6. SNAPSHOTS / COPIAS DE SEGURIDAD (EBS Y RDS)"
echo "========================================================"
aws ec2 describe-snapshots \
  --owner-ids self \
  --query "Snapshots[*].{SnapshotId:SnapshotId, SizeGB:VolumeSize, Fecha:StartTime}" \
  --output table

aws rds describe-db-snapshots \
  --query "DBSnapshots[*].{SnapshotID:DBSnapshotIdentifier, Engine:Engine}" \
  --output table

aws rds describe-db-cluster-snapshots \
  --query "DBClusterSnapshots[*].{ClusterSnapshot:DBClusterSnapshotIdentifier, Engine:Engine}" \
  --output table

echo ""
echo "========================================================"
echo "⚖️  7. BALANCEADORES DE CARGA (ALB/NLB) Y NAT GATEWAYS"
echo "========================================================"
aws elbv2 describe-load-balancers \
  --query "LoadBalancers[*].{Nombre:LoadBalancerName, DNS:DNSName, Estado:State.Code}" \
  --output table

aws ec2 describe-nat-gateways \
  --filter "Name=state,Values=available,pending" \
  --query "NatGateways[*].{ID:NatGatewayId, VPC:VpcId, Estado:State}" \
  --output table

echo ""
echo "========================================================"
echo "📦 8. BUCKETS DE S3"
echo "========================================================"
aws s3 ls

echo ""
echo "========================================================"
echo "☸️  9. CLUSTERS EKS / ECS (Contenedores gestionados)"
echo "========================================================"
aws eks list-clusters --output table
aws ecs list-clusters --output table
