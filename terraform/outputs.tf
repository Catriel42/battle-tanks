output "s3_bucket_name" {
  description = "Name of the S3 bucket created for frontend assets"
  value       = aws_s3_bucket.frontend.id
}

output "cloudfront_domain_name" {
  description = "CloudFront distribution domain name (Use this URL to access your game)"
  value       = "https://${aws_cloudfront_distribution.cdn.domain_name}"
}

output "cloudfront_distribution_id" {
  description = "CloudFront distribution ID (used for invalidating cache after deployments)"
  value       = aws_cloudfront_distribution.cdn.id
}

output "deploy_command_hint" {
  description = "Command to sync built Angular frontend to S3"
  value       = "aws s3 sync ../BattleTanks-Frontend/dist/battle-tanks-frontend/browser s3://${aws_s3_bucket.frontend.id} --delete"
}

# -------------------------------------------------------------
# Outputs para el Backend / Servidor EC2
# -------------------------------------------------------------
output "ec2_public_ip" {
  description = "IP pública estática (Elastic IP) del servidor EC2"
  value       = aws_eip.backend_ip.public_ip
}

output "ec2_ssh_command" {
  description = "Comando para conectarte directo por SSH usando tu llave"
  value       = "ssh -i ~/.ssh/ec2_keys ubuntu@${aws_eip.backend_ip.public_ip}"
}

output "backend_api_url" {
  description = "URL de la API en el servidor"
  value       = "http://${aws_eip.backend_ip.public_ip}:5000"
}

output "emqx_dashboard_url" {
  description = "Panel de control de EMQX (usuario: admin / password: public)"
  value       = "http://${aws_eip.backend_ip.public_ip}:18083"
}

output "grafana_dashboard_url" {
  description = "Panel de Grafana (usuario: admin / password: battletanks)"
  value       = "http://${aws_eip.backend_ip.public_ip}:3000"
}

