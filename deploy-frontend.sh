#!/bin/bash
set -e

echo "=== 1. Compilando Frontend para Producción ==="
cd BattleTanks-Frontend
npm run build -- --configuration production
cd ..

echo "=== 2. Obteniendo información de Terraform ==="
cd terraform
BUCKET_NAME=$(terraform output -raw s3_bucket_name)
DISTRIBUTION_ID=$(terraform output -raw cloudfront_distribution_id)
cd ..

echo "=== 3. Sincronizando artefactos con S3 (${BUCKET_NAME}) ==="
aws s3 sync BattleTanks-Frontend/dist/BattleTanks-Frontend/browser s3://${BUCKET_NAME} --delete

echo "=== 4. Invalidando caché de CloudFront (${DISTRIBUTION_ID}) ==="
aws cloudfront create-invalidation --distribution-id ${DISTRIBUTION_ID} --paths "/*"

echo "=== Despliegue completado con éxito ==="
