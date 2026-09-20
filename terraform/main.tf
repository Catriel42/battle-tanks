terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region = var.aws_region
}

# -------------------------------------------------------------
# 1. Bucket S3 Privado para el Frontend (SPA)
# -------------------------------------------------------------
resource "aws_s3_bucket" "frontend" {
  bucket_prefix = "${var.project_name}-frontend-"
  force_destroy = true
}

resource "aws_s3_bucket_public_access_block" "frontend" {
  bucket = aws_s3_bucket.frontend.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# -------------------------------------------------------------
# 2. CloudFront Origin Access Control (OAC)
# -------------------------------------------------------------
resource "aws_cloudfront_origin_access_control" "oac" {
  name                              = "${var.project_name}-oac"
  description                       = "OAC for BattleTanks S3 bucket"
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
}

# -------------------------------------------------------------
# 3. Política de S3 para dar acceso EXCLUSIVO a CloudFront
# -------------------------------------------------------------
data "aws_iam_policy_document" "s3_policy" {
  statement {
    actions   = ["s3:GetObject"]
    resources = ["${aws_s3_bucket.frontend.arn}/*"]

    principals {
      type        = "Service"
      identifiers = ["cloudfront.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "AWS:SourceArn"
      values   = [aws_cloudfront_distribution.cdn.arn]
    }
  }
}

resource "aws_s3_bucket_policy" "frontend" {
  bucket = aws_s3_bucket.frontend.id
  policy = data.aws_iam_policy_document.s3_policy.json
}

# -------------------------------------------------------------
# 4. Distribución de CloudFront
# -------------------------------------------------------------
resource "aws_cloudfront_distribution" "cdn" {
  enabled             = true
  is_ipv6_enabled     = true
  default_root_object = "index.html"
  comment             = "BattleTanks Frontend CDN"

  origin {
    domain_name              = aws_s3_bucket.frontend.bucket_regional_domain_name
    origin_id                = "S3-${aws_s3_bucket.frontend.id}"
    origin_access_control_id = aws_cloudfront_origin_access_control.oac.id
  }

  # Origen secundario: Tu servidor EC2 (Nginx Reverse Proxy en puerto 80)
  origin {
    domain_name = aws_eip.backend_ip.public_dns
    origin_id   = "EC2-${aws_instance.backend_server.id}"

    custom_origin_config {
      http_port              = 80
      https_port             = 443
      origin_protocol_policy = "http-only"
      origin_ssl_protocols   = ["TLSv1.2"]
    }
  }

  # Regla para /api/* (Llamadas REST al Backend)
  ordered_cache_behavior {
    path_pattern     = "/api/*"
    allowed_methods  = ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"]
    cached_methods   = ["GET", "HEAD", "OPTIONS"]
    target_origin_id = "EC2-${aws_instance.backend_server.id}"

    forwarded_values {
      query_string = true
      headers      = ["Accept", "Authorization", "Content-Type", "Host", "Origin", "Referer"]
      cookies {
        forward = "all"
      }
    }

    min_ttl                = 0
    default_ttl            = 0
    max_ttl                = 0
    viewer_protocol_policy = "redirect-to-https"
  }

  # Regla para /gamehub/* (SignalR WebSockets y SSE)
  ordered_cache_behavior {
    path_pattern     = "/gamehub*"
    allowed_methods  = ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"]
    cached_methods   = ["GET", "HEAD", "OPTIONS"]
    target_origin_id = "EC2-${aws_instance.backend_server.id}"

    forwarded_values {
      query_string = true
      headers      = [
        "Accept",
        "Authorization",
        "Content-Type",
        "Host",
        "Origin",
        "Referer",
        "Sec-WebSocket-Key",
        "Sec-WebSocket-Version",
        "Sec-WebSocket-Protocol",
        "Sec-WebSocket-Extensions"
      ]
      cookies {
        forward = "all"
      }
    }

    min_ttl                = 0
    default_ttl            = 0
    max_ttl                = 0
    viewer_protocol_policy = "redirect-to-https"
  }

  # Regla para /mqtt (EMQX MQTT WebSockets)
  ordered_cache_behavior {
    path_pattern     = "/mqtt*"
    allowed_methods  = ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"]
    cached_methods   = ["GET", "HEAD", "OPTIONS"]
    target_origin_id = "EC2-${aws_instance.backend_server.id}"

    forwarded_values {
      query_string = true
      headers      = [
        "Host",
        "Origin",
        "Sec-WebSocket-Key",
        "Sec-WebSocket-Version",
        "Sec-WebSocket-Protocol",
        "Sec-WebSocket-Extensions"
      ]
      cookies {
        forward = "all"
      }
    }

    min_ttl                = 0
    default_ttl            = 0
    max_ttl                = 0
    viewer_protocol_policy = "redirect-to-https"
  }

  default_cache_behavior {
    allowed_methods  = ["GET", "HEAD", "OPTIONS"]
    cached_methods   = ["GET", "HEAD"]
    target_origin_id = "S3-${aws_s3_bucket.frontend.id}"

    forwarded_values {
      query_string = false
      cookies {
        forward = "none"
      }
    }

    viewer_protocol_policy = "redirect-to-https"
    min_ttl                = 0
    default_ttl            = 3600
    max_ttl                = 86400
    compress               = true
  }

  # Soporte para Angular SPA Routing (Redirigir 403 y 404 a index.html)
  custom_error_response {
    error_code            = 403
    response_code         = 200
    response_page_path    = "/index.html"
    error_caching_min_ttl = 10
  }

  custom_error_response {
    error_code            = 404
    response_code         = 200
    response_page_path    = "/index.html"
    error_caching_min_ttl = 10
  }

  price_class = "PriceClass_100" # Usa solo Norteamérica y Europa para menor costo

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  viewer_certificate {
    cloudfront_default_certificate = true
  }

  tags = {
    Project     = var.project_name
    Environment = "production"
  }
}

# -------------------------------------------------------------
# 5. Par de Llaves SSH (Registra ~/.ssh/ec2_keys.pub en AWS)
# -------------------------------------------------------------
resource "aws_key_pair" "deployer" {
  key_name   = "${var.project_name}-key"
  public_key = file(pathexpand(var.ssh_public_key_path))

  tags = {
    Project = var.project_name
  }
}

# -------------------------------------------------------------
# 6. Búsqueda de la AMI oficial más reciente de Ubuntu 24.04 LTS
# -------------------------------------------------------------
data "aws_ami" "ubuntu" {
  most_recent = true
  owners      = ["099720109477"] # Canonical (dueños oficiales de Ubuntu)

  filter {
    name   = "name"
    values = ["ubuntu/images/hvm-ssd-gp3/ubuntu-noble-24.04-amd64-server-*"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

# -------------------------------------------------------------
# 7. Security Group (Firewall para la máquina virtual)
# -------------------------------------------------------------
resource "aws_security_group" "ec2_sg" {
  name        = "${var.project_name}-ec2-sg"
  description = "Reglas de firewall para BattleTanks Backend e Infraestructura"

  # SSH
  ingress {
    description = "Acceso SSH administrativo"
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # HTTP / HTTPS (para Nginx / Certbot futuro)
  ingress {
    description = "HTTP"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "HTTPS"
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # API .NET y SignalR Hub
  ingress {
    description = "BattleTanks API REST y SignalR Hub"
    from_port   = 5000
    to_port     = 5000
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # EMQX MQTT (WebSocket para navegador y TCP)
  ingress {
    description = "EMQX MQTT WebSocket Frontend"
    from_port   = 8083
    to_port     = 8083
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "EMQX MQTT TCP standard"
    from_port   = 1883
    to_port     = 1883
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "EMQX Dashboard Web"
    from_port   = 18083
    to_port     = 18083
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # pgAdmin & Grafana (Administracion y Metricas)
  ingress {
    description = "pgAdmin Web"
    from_port   = 8081
    to_port     = 8081
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "Grafana Web"
    from_port   = 3000
    to_port     = 3000
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # Egress: Salida a internet sin restricciones (para apt-get, docker pull, nuget)
  egress {
    from_port        = 0
    to_port          = 0
    protocol         = "-1"
    cidr_blocks      = ["0.0.0.0/0"]
    ipv6_cidr_blocks = ["::/0"]
  }

  tags = {
    Project = var.project_name
  }
}

# -------------------------------------------------------------
# 8. Instancia EC2 (Ubuntu 24.04 + Docker auto-instalado)
# -------------------------------------------------------------
resource "aws_instance" "backend_server" {
  ami                    = data.aws_ami.ubuntu.id
  instance_type          = var.instance_type
  key_name               = aws_key_pair.deployer.key_name
  vpc_security_group_ids = [aws_security_group.ec2_sg.id]

  root_block_device {
    volume_size           = var.root_volume_size
    volume_type           = "gp3"
    delete_on_termination = true
  }

  # Script de inicialización que corre en el primer arranque de la máquina
  user_data = <<-EOF
              #!/bin/bash
              set -e
              export DEBIAN_FRONTEND=noninteractive

              # 1. Actualizar paquetes base
              apt-get update -y
              apt-get upgrade -y
              apt-get install -y ca-certificates curl gnupg lsb-release git htop

              # 2. Instalar Docker oficial
              install -m 0755 -d /etc/apt/keyrings
              curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
              chmod a+r /etc/apt/keyrings/docker.asc

              echo \
                "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \
                $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
                tee /etc/apt/sources.list.d/docker.list > /dev/null

              apt-get update -y
              apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

              # 3. Dar permisos al usuario ubuntu para correr docker sin sudo
              usermod -aG docker ubuntu
              systemctl enable docker
              systemctl start docker
              EOF

  tags = {
    Name    = "${var.project_name}-backend-server"
    Project = var.project_name
  }
}

# -------------------------------------------------------------
# 9. Elastic IP (IP fija que nunca cambia al reiniciar)
# -------------------------------------------------------------
resource "aws_eip" "backend_ip" {
  instance = aws_instance.backend_server.id
  domain   = "vpc"

  tags = {
    Name    = "${var.project_name}-eip"
    Project = var.project_name
  }
}

