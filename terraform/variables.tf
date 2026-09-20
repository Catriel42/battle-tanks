variable "aws_region" {
  description = "AWS region to deploy resources"
  type        = string
  default     = "us-east-1"
}

variable "project_name" {
  description = "Project name prefix for resources"
  type        = string
  default     = "battletanks"
}

variable "instance_type" {
  description = "EC2 instance type (t3.medium recommended for 4GB RAM)"
  type        = string
  default     = "t3.medium"
}

variable "ssh_public_key_path" {
  description = "Path to local SSH public key to register in AWS"
  type        = string
  default     = "~/.ssh/ec2_keys.pub"
}

variable "root_volume_size" {
  description = "Root disk size in GB"
  type        = number
  default     = 30
}

