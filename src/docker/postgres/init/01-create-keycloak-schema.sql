-- Initialize Keycloak schema
-- This script runs automatically when PostgreSQL container starts for the first time

CREATE SCHEMA IF NOT EXISTS keycloak;

-- Grant necessary permissions
GRANT ALL PRIVILEGES ON SCHEMA keycloak TO postgres;
