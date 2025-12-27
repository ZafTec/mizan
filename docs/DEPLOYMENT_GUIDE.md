# MacroChef Deployment Guide

**Version:** 1.0
**Last Updated:** 2025-12-27

---

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Environment Configuration](#environment-configuration)
- [Docker Deployment](#docker-deployment)
- [Database Setup](#database-setup)
- [SSL/TLS Configuration](#ssltls-configuration)
- [Backup and Restore](#backup-and-restore)
- [Monitoring](#monitoring)
- [Scaling](#scaling)
- [Troubleshooting](#troubleshooting)

---

## Overview

MacroChef is deployed using Docker Compose with four main services:
- **PostgreSQL** - Database
- **Redis** - Cache and SignalR backplane
- **Backend** - ASP.NET Core 10 API
- **Frontend** - Next.js 16 application

---

## Prerequisites

### System Requirements

**Minimum (Development):**
- CPU: 2 cores
- RAM: 4GB
- Disk: 10GB

**Recommended (Production):**
- CPU: 4+ cores
- RAM: 8GB+
- Disk: 50GB+ SSD

### Software Requirements

- Docker Engine 24.0+
- Docker Compose 2.20+
- Git

**Install Docker:**
```bash
# Ubuntu/Debian
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER

# Verify installation
docker --version
docker-compose --version
```

---

## Environment Configuration

### Frontend Environment Variables

Create `frontend/.env.production`:

```bash
# Database (PostgreSQL for BetterAuth)
DATABASE_URL=postgresql://mizan:CHANGE_THIS_PASSWORD@postgres:5432/mizan

# BetterAuth Configuration
BETTER_AUTH_SECRET=GENERATE_STRONG_SECRET_HERE_AT_LEAST_32_CHARS
BETTER_AUTH_URL=https://yourdomain.com
BETTER_AUTH_TRUST_HOST=true

# API URLs
API_URL=http://mizan-backend:8080
NEXT_PUBLIC_API_URL=https://yourdomain.com

# BFF Secret (must match backend)
BFF_SECRET=GENERATE_ANOTHER_STRONG_SECRET_HERE

# Node Environment
NODE_ENV=production
```

**Generate Secrets:**
```bash
# Linux/Mac
openssl rand -base64 32

# Or use Node.js
node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"
```

### Backend Environment Variables

Create `backend/.env.production`:

```bash
# ASP.NET Environment
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080

# Database
ConnectionStrings__PostgreSQL=Host=postgres;Port=5432;Database=mizan;Username=mizan;Password=CHANGE_THIS_PASSWORD;SSL Mode=Prefer

# Redis
ConnectionStrings__Redis=redis:6379,abortConnect=false

# BetterAuth Integration
BetterAuth__JwksUrl=https://yourdomain.com/api/auth/jwks
BetterAuth__Issuer=https://yourdomain.com
BetterAuth__Audience=https://yourdomain.com

# BFF Secret (must match frontend)
Bff__TrustedSecret=SAME_SECRET_AS_FRONTEND_BFF_SECRET

# CORS (optional, for direct API access)
CORS__AllowedOrigins__0=https://yourdomain.com
```

### Docker Compose Environment

Create `.env` in project root:

```bash
# PostgreSQL
POSTGRES_USER=mizan
POSTGRES_PASSWORD=CHANGE_THIS_PASSWORD
POSTGRES_DB=mizan

# Ports (change if conflicts exist)
POSTGRES_PORT=5432
REDIS_PORT=6379
BACKEND_PORT=5000
FRONTEND_PORT=3000
```

---

## Docker Deployment

### Production Docker Compose

Create `docker-compose.prod.yml`:

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:18-alpine
    container_name: mizan-postgres
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    networks:
      - mizan-network
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER}"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    container_name: mizan-redis
    volumes:
      - redis_data:/data
    networks:
      - mizan-network
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  backend:
    build:
      context: ./backend
      dockerfile: Dockerfile
    container_name: mizan-backend
    env_file:
      - backend/.env.production
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    networks:
      - mizan-network
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    container_name: mizan-frontend
    env_file:
      - frontend/.env.production
    ports:
      - "${FRONTEND_PORT}:3000"
    depends_on:
      - backend
    networks:
      - mizan-network
    restart: unless-stopped

volumes:
  postgres_data:
  redis_data:

networks:
  mizan-network:
    driver: bridge
```

### Build and Deploy

```bash
# 1. Clone repository
git clone https://github.com/yourusername/macrochef.git
cd macrochef

# 2. Create environment files
cp frontend/.env.example frontend/.env.production
cp backend/.env.example backend/.env.production
nano frontend/.env.production  # Edit with production values
nano backend/.env.production    # Edit with production values

# 3. Build images
docker-compose -f docker-compose.prod.yml build

# 4. Start services
docker-compose -f docker-compose.prod.yml up -d

# 5. View logs
docker-compose -f docker-compose.prod.yml logs -f

# 6. Check status
docker-compose -f docker-compose.prod.yml ps
```

---

## Database Setup

### Initial Migration

**Backend (EF Core):**
```bash
# Run migrations automatically on startup
# Or manually:
docker exec -it mizan-backend dotnet ef database update
```

**Frontend (Drizzle):**
```bash
# Access frontend container
docker exec -it mizan-frontend sh

# Run migrations
bun run db:migrate
```

### Create First Admin User

```bash
# Access PostgreSQL
docker exec -it mizan-postgres psql -U mizan -d mizan

# Sign up via UI first, then promote to admin:
UPDATE users SET role = 'admin' WHERE email = 'admin@yourdomain.com';
```

---

## SSL/TLS Configuration

### Nginx Reverse Proxy

Create `nginx.conf`:

```nginx
upstream backend {
    server localhost:5000;
}

upstream frontend {
    server localhost:3000;
}

server {
    listen 80;
    server_name yourdomain.com;
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name yourdomain.com;

    ssl_certificate /etc/letsencrypt/live/yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/yourdomain.com/privkey.pem;

    # SSL configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;

    # Frontend
    location / {
        proxy_pass http://frontend;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }

    # Backend API
    location /api {
        proxy_pass http://backend;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # SignalR WebSocket
    location /hubs {
        proxy_pass http://backend;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "Upgrade";
        proxy_set_header Host $host;
    }
}
```

### Let's Encrypt SSL

```bash
# Install Certbot
sudo apt install certbot python3-certbot-nginx

# Obtain certificate
sudo certbot --nginx -d yourdomain.com

# Auto-renewal (cron)
sudo crontab -e
# Add line:
0 12 * * * /usr/bin/certbot renew --quiet
```

---

## Backup and Restore

### Database Backup

**Automated Backup Script:**

Create `scripts/backup.sh`:

```bash
#!/bin/bash

BACKUP_DIR="/backups"
DATE=$(date +%Y%m%d_%H%M%S)
DB_NAME="mizan"

# PostgreSQL backup
docker exec mizan-postgres pg_dump -U mizan $DB_NAME | gzip > $BACKUP_DIR/postgres_$DATE.sql.gz

# Redis backup
docker exec mizan-redis redis-cli SAVE
docker cp mizan-redis:/data/dump.rdb $BACKUP_DIR/redis_$DATE.rdb

# Cleanup old backups (keep 30 days)
find $BACKUP_DIR -name "postgres_*.sql.gz" -mtime +30 -delete
find $BACKUP_DIR -name "redis_*.rdb" -mtime +30 -delete

echo "Backup completed: $DATE"
```

**Schedule with Cron:**
```bash
chmod +x scripts/backup.sh
crontab -e
# Add daily backup at 2 AM:
0 2 * * * /path/to/macrochef/scripts/backup.sh
```

### Restore from Backup

```bash
# Restore PostgreSQL
gunzip < /backups/postgres_20251227_020000.sql.gz | docker exec -i mizan-postgres psql -U mizan mizan

# Restore Redis
docker cp /backups/redis_20251227_020000.rdb mizan-redis:/data/dump.rdb
docker restart mizan-redis
```

---

## Monitoring

### Health Checks

**Frontend Health:**
```bash
curl https://yourdomain.com/api/health
```

**Backend Health:**
```bash
curl https://yourdomain.com/health
```

**Expected Response:**
```json
{
  "status": "Healthy",
  "checks": {
    "PostgreSQL": "Healthy",
    "Redis": "Healthy"
  }
}
```

### Log Management

**View Service Logs:**
```bash
# All services
docker-compose -f docker-compose.prod.yml logs -f

# Specific service
docker-compose -f docker-compose.prod.yml logs -f backend
docker-compose -f docker-compose.prod.yml logs -f frontend
```

**Log Rotation:**

Create `/etc/logrotate.d/docker-containers`:

```
/var/lib/docker/containers/*/*.log {
  rotate 7
  daily
  compress
  delaycompress
  missingok
  notifempty
  copytruncate
}
```

---

## Scaling

### Horizontal Scaling (Multiple Instances)

**Update docker-compose.prod.yml:**

```yaml
services:
  backend:
    # ... existing config
    deploy:
      replicas: 3

  frontend:
    # ... existing config
    deploy:
      replicas: 2
```

**Nginx Load Balancing:**

```nginx
upstream backend {
    least_conn;
    server backend-1:8080;
    server backend-2:8080;
    server backend-3:8080;
}

upstream frontend {
    least_conn;
    server frontend-1:3000;
    server frontend-2:3000;
}
```

### Database Scaling

**Read Replicas:**

Add to `docker-compose.prod.yml`:

```yaml
services:
  postgres-replica:
    image: postgres:18-alpine
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_PRIMARY_HOST: postgres
      POSTGRES_PRIMARY_PORT: 5432
    volumes:
      - postgres_replica_data:/var/lib/postgresql/data
```

**Connection Pooling (PgBouncer):**

```yaml
services:
  pgbouncer:
    image: pgbouncer/pgbouncer
    environment:
      DATABASES_HOST: postgres
      DATABASES_PORT: 5432
      DATABASES_DBNAME: mizan
    ports:
      - "6432:6432"
```

---

## Troubleshooting

### Common Issues

**Issue: "Connection refused" to database**

```bash
# Check PostgreSQL is running
docker ps | grep postgres

# Check logs
docker logs mizan-postgres

# Verify network
docker network inspect mizan-network
```

**Issue: "JWKS endpoint not accessible"**

```bash
# Test JWKS endpoint
curl https://yourdomain.com/api/auth/jwks

# Check Redis cache
docker exec -it mizan-redis redis-cli
> KEYS "jwks:*"

# Clear JWKS cache
> DEL jwks:https://yourdomain.com/api/auth/jwks
```

**Issue: SignalR connection failing**

```bash
# Check WebSocket support
curl -i -N -H "Connection: Upgrade" -H "Upgrade: websocket" \
  https://yourdomain.com/hubs/chat

# Verify Redis backplane
docker logs mizan-redis | grep SignalR
```

**Issue: High memory usage**

```bash
# Check container stats
docker stats

# Restart service
docker-compose -f docker-compose.prod.yml restart backend

# Limit memory in docker-compose.prod.yml:
services:
  backend:
    deploy:
      resources:
        limits:
          memory: 1G
```

### Debug Mode

**Enable verbose logging:**

`backend/.env.production`:
```bash
Logging__LogLevel__Default=Debug
Logging__LogLevel__Microsoft=Information
```

`frontend/.env.production`:
```bash
NEXT_PUBLIC_LOG_LEVEL=debug
```

---

## Security Checklist

- [ ] Change all default passwords
- [ ] Generate strong secrets (32+ characters)
- [ ] Enable SSL/TLS with valid certificates
- [ ] Configure firewall (allow only 80/443)
- [ ] Disable unnecessary ports
- [ ] Set up automated backups
- [ ] Enable log monitoring
- [ ] Regularly update Docker images
- [ ] Review user permissions
- [ ] Test disaster recovery plan

---

## Maintenance

### Update Application

```bash
# 1. Pull latest code
git pull origin master

# 2. Rebuild images
docker-compose -f docker-compose.prod.yml build

# 3. Stop services
docker-compose -f docker-compose.prod.yml down

# 4. Run migrations
docker-compose -f docker-compose.prod.yml run backend dotnet ef database update
docker-compose -f docker-compose.prod.yml run frontend bun run db:migrate

# 5. Start services
docker-compose -f docker-compose.prod.yml up -d

# 6. Verify health
curl https://yourdomain.com/api/health
```

### Database Maintenance

**Vacuum PostgreSQL:**
```bash
docker exec -it mizan-postgres psql -U mizan -d mizan -c "VACUUM ANALYZE;"
```

**Clear Redis Cache:**
```bash
docker exec -it mizan-redis redis-cli FLUSHDB
```

---

## Performance Optimization

### Database Indexing

```sql
-- Add indexes for common queries
CREATE INDEX idx_recipes_userid ON recipes(user_id);
CREATE INDEX idx_mealplans_userid ON meal_plans(user_id);
CREATE INDEX idx_fooddiary_userid_date ON food_diary_entries(user_id, date);
```

### Redis Tuning

`redis.conf`:
```
maxmemory 2gb
maxmemory-policy allkeys-lru
save 900 1
save 300 10
```

### Next.js Optimization

Build with production optimizations:
```bash
cd frontend
bun run build
bun run start
```

---

## Support

For deployment issues:
- Check logs: `docker-compose logs`
- Review health checks
- Consult SECURITY.md for security-related issues
- Consult TROUBLESHOOTING section above

For feature requests or bugs:
- GitHub Issues: https://github.com/yourusername/macrochef/issues
