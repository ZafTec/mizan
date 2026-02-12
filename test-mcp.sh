#!/bin/bash

# MCP Server Test Script
# This script tests the MCP server endpoints

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "========================================"
echo "MCP Server Local Testing Script"
echo "========================================"
echo ""

# Check if Docker is running
if ! docker ps > /dev/null 2>&1; then
    echo -e "${RED}✗ Docker is not running. Please start Docker Desktop first.${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Docker is running${NC}"

# Check if services are running
echo ""
echo "Checking services..."
echo "--------------------"

POSTGRES_RUNNING=$(docker ps --filter "name=mizan-postgres" --format "{{.Names}}" | grep -c "mizan-postgres" || true)
BACKEND_RUNNING=$(docker ps --filter "name=mizan-backend" --format "{{.Names}}" | grep -c "mizan-backend" || true)
MCP_RUNNING=$(docker ps --filter "name=mizan-mcp" --format "{{.Names}}" | grep -c "mizan-mcp" || true)

if [ "$POSTGRES_RUNNING" -eq 0 ]; then
    echo -e "${YELLOW}⚠ PostgreSQL not running. Starting...${NC}"
    docker-compose up -d postgres
    sleep 5
fi
echo -e "${GREEN}✓ PostgreSQL running${NC}"

if [ "$BACKEND_RUNNING" -eq 0 ]; then
    echo -e "${YELLOW}⚠ Backend not running. Starting...${NC}"
    docker-compose up -d backend
    sleep 10
fi
echo -e "${GREEN}✓ Backend running${NC}"

if [ "$MCP_RUNNING" -eq 0 ]; then
    echo -e "${YELLOW}⚠ MCP server not running. Starting...${NC}"
    docker-compose up -d mcp
    sleep 5
fi
echo -e "${GREEN}✓ MCP server running${NC}"

# Test MCP server endpoints
echo ""
echo "Testing MCP Server..."
echo "--------------------"

# Test 1: Check if MCP server is listening
echo -n "Testing MCP server port (5001)... "
if nc -zv localhost 5001 > /dev/null 2>&1; then
    echo -e "${GREEN}✓ OK${NC}"
else
    echo -e "${RED}✗ FAILED${NC}"
    echo "MCP server is not accepting connections on port 5001"
    exit 1
fi

# Test 2: Check backend is accessible from MCP container
echo -n "Testing backend connectivity from MCP... "
if docker-compose exec -T mcp nc -zv backend 8080 > /dev/null 2>&1; then
    echo -e "${GREEN}✓ OK${NC}"
else
    echo -e "${YELLOW}⚠ WARNING${NC}"
    echo "  Backend may not be accessible from MCP container"
fi

# Test 3: Check database connectivity
echo -n "Testing database connectivity from MCP... "
if docker-compose exec -T mcp nc -zv postgres 5432 > /dev/null 2>&1; then
    echo -e "${GREEN}✓ OK${NC}"
else
    echo -e "${YELLOW}⚠ WARNING${NC}"
    echo "  Database may not be accessible from MCP container"
fi

# Display container status
echo ""
echo "Container Status"
echo "----------------"
docker-compose ps --format "table {{.Name}}\t{{.Status}}\t{{.Ports}}" 2>/dev/null | grep -E "(mizan-|NAME)" || true

# Display helpful information
echo ""
echo "========================================"
echo "Test Complete!"
echo "========================================"
echo ""
echo "MCP Server is running at: http://localhost:5001"
echo ""
echo "To test with a real token:"
echo "  1. Start the frontend: cd frontend && bun run dev"
echo "  2. Visit http://localhost:3000 and login"
echo "  3. Go to Profile → MCP Integration"
echo "  4. Generate a token"
echo "  5. Test SSE connection:"
echo "     curl -N -H 'Authorization: Bearer YOUR_TOKEN' http://localhost:5001/mcp/sse"
echo ""
echo "To view logs:"
echo "  docker-compose logs -f mcp"
echo ""
echo "To stop all services:"
echo "  docker-compose down"
echo ""
