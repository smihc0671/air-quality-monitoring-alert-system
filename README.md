# Air Quality Monitoring and Alert System

A web-based air quality monitoring and database management system developed for the **CMPE 232 – Database Systems** course at **TED University**.

The application stores and analyzes global Air Quality Index (AQI) data, manages country and city information, monitors pollutant-based measurements, and automatically generates alerts for potentially hazardous air-quality conditions.

## Project Overview

The system provides separate **User** and **Admin** functionality.

Users can explore countries, cities, air-quality records and generated alerts using search, filtering, sorting and pagination features.

Administrators can additionally create, update and delete database records through the web interface.

📄 **[View Full Visual Project Overview](docs/Air_Quality_Monitoring_Alert_System_Project_Overview.pdf)**

## Features

- Global air quality monitoring
- Country and city management
- Air Quality Index (AQI) analysis
- Pollutant-based air-quality records
- Automatic alert generation
- AQI category classification
- User and Admin roles
- Search, filtering, sorting and pagination
- Administrative CRUD operations
- Dashboard statistics and visualizations
- Relational database design
- Entity Framework database integration

## Technologies

- C#
- ASP.NET MVC
- Microsoft SQL Server
- Entity Framework
- LINQ
- Razor Views
- HTML
- CSS
- JavaScript
- Bootstrap
- jQuery

## Database Design

The relational database includes the following main entities:

- Country
- City
- Pollutant
- GasPollutant
- ParticulatePollutant
- AQICategory
- AirQualityRecord
- Alert
- AlertNote
- RawAirQualityImport

`AirQualityRecord` acts as the central entity connecting cities, pollutants and AQI categories.

The database design uses:

- Primary keys
- Foreign keys
- Unique constraints
- Check constraints
- Referential integrity
- SQL triggers
- Normalization up to Third Normal Form (3NF)

## Application Structure

```text
AirQualityAnalysis
├── App_Start
├── Content
├── Controllers
├── Filters
├── Properties
├── Scripts
├── Views
├── docs
├── Entity Framework Models
├── Web.config
└── AirQualityAnalysis.csproj
```

## Main Application Pages

The application includes:

- Login
- Dashboard
- Countries
- Cities
- Air Quality Records
- Alerts
- Administrative management pages

The dashboard provides dataset statistics, AQI comparisons and generated-alert distributions.

## Data Source

The project uses air-quality data derived from the **Global Air Pollution Dataset** available on Kaggle.

The dataset contains AQI-based measurements for countries and cities around the world.

## Documentation

Detailed documentation for the project is available in the `docs` directory.

- 📄 **[Visual Project Overview](docs/Air_Quality_Monitoring_Alert_System_Project_Overview.pdf)**  
  Screenshots and a visual overview of the completed web application.

- 📘 **[Technical Report](docs/Air_Quality_Monitoring_Alert_System_Technical_Report.pdf)**  
  Detailed explanation of the ER model, relational schema, normalization, database constraints, SQL triggers, data-import workflow and alert-generation logic.

## Dataset

The project is based on the **Global Air Pollution Dataset** from Kaggle.

The original dataset is not redistributed directly in this repository. Information about the dataset, its structure and source is available here:

**[Dataset Information](data/README.md)**
## Academic Context

**Course:** CMPE 232 – Database Systems  
**University:** TED University  
**Semester:** Spring 2026

### Team

- Beyza Yağmur Er
- Semih Can Kasar
- Güner Tugay Arısoy

## Purpose

This project was developed to demonstrate the design and implementation of a relational database-backed web application, including database normalization, entity relationships, data integrity, querying, role-based operations and user-interface integration.