# Authenticate - ASP.NET Core Authentication System

## Overview
This is an ASP.NET Core 9.0 MVC web application that provides user authentication, API credential management, and various backend services. The application has been successfully configured to run in the Replit environment.

## Recent Changes
- **2025-09-26**: Successfully imported and configured the project for Replit environment
- **2025-09-26**: Set up automatic Entity Framework migrations on startup
- **2025-09-26**: Configured PostgreSQL database connection using Replit's managed database
- **2025-09-26**: Configured deployment settings for production

## Project Architecture

### Backend
- **Framework**: ASP.NET Core 9.0 MVC
- **Database**: PostgreSQL with Entity Framework Core
- **Authentication**: Cookie-based authentication
- **Language**: C# with .NET 9.0

### Frontend
- **Type**: Server-side rendered views (Razor Pages)
- **UI Framework**: Bootstrap
- **JavaScript**: jQuery with validation

### Key Features
- User registration and authentication
- API credential management (supports various auth types: API Key, Basic Auth, Cookies)
- Admin dashboard
- NFL team data synchronization
- Email functionality (SMTP)
- Gemini AI integration
- Comprehensive logging system

### Database Tables
The application includes the following main entities:
- Users and authentication (Users, Passwords, Emails)
- API credentials and types
- Menu system
- NFL teams data
- Application logs
- Password reset functionality

### Environment Configuration
The application is configured to use the following environment variables:
- `PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`, `PGPASSWORD` - PostgreSQL connection
- `SMTP_*` variables for email configuration (optional)
- `GEMINI_*` variables for AI integration (optional)
- `DB_LOGGER_MINLEVEL` for logging level control

### Development Setup
1. The application runs on port 5000 (configured for Replit)
2. Entity Framework migrations are applied automatically on startup
3. The database connection is built from PostgreSQL environment variables
4. Static assets are served from wwwroot/

### Deployment
- **Target**: Autoscale deployment (suitable for stateless web applications)
- **Build**: `dotnet build --configuration Release`
- **Run**: `dotnet run --configuration Release --no-launch-profile --urls http://0.0.0.0:5000 --environment Production`

## User Preferences
- No specific user preferences documented yet

## Notes
- The application includes comprehensive error handling and logging
- Database migrations are managed through Entity Framework Code-First approach
- The project structure follows standard ASP.NET Core MVC conventions
- All sensitive configuration is handled through environment variables