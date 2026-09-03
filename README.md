# Training Center Web API

ASP.NET Core Web API for managing a training center's users, students, instructors, courses, enrollments, and student profiles.

## Technology stack

- .NET 8 / ASP.NET Core Web API
- Entity Framework Core 8 with SQL Server
- JWT bearer authentication with refresh-token rotation
- AutoMapper
- Swagger/OpenAPI
- Repository and Unit of Work patterns

## Prerequisites

- .NET 8 SDK
- SQL Server or LocalDB
- Optional: a REST client such as Swagger UI, Postman, or the VS Code REST Client

## Getting started

From the directory containing `TrainingCenterWebAPI.csproj`:

1. Restore dependencies:

   ```bash
   dotnet restore
   ```

2. Configure SQL Server and JWT settings as described below.

3. Create and apply the database schema. The `Migrations` directory is currently empty and the application does not migrate the database automatically:

   ```bash
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

4. Start the API:

   ```bash
   dotnet run
   ```

When running with the Development profile, Swagger UI is available at:

- `http://localhost:5294/swagger`
- `https://localhost:7277/swagger`

Build the solution from its parent directory with `dotnet build TrainingCenterWebAPI.slnx`.

## Configuration

The default configuration in `appsettings.json` uses a local SQL Server instance with Windows authentication:

```text
Server=.;Database=TrainingCenterDB;Trusted_Connection=True;TrustServerCertificate=True;
```

Update `ConnectionStrings:DefaultConnection` if your SQL Server instance is different. Environment variables can override JSON configuration, for example:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Key`
- `Jwt__Issuer`
- `Jwt__Audience`

Replace the development JWT key with a long, randomly generated secret before sharing or deploying the application. Do not commit production secrets to source control. HTTPS metadata validation is currently relaxed for local development and should be enabled in production.

JWT settings use the issuer `TrainingCenterAPI` and audience `TrainingCenterClients`. Access tokens are short-lived, while refresh tokens are stored hashed and rotated when refreshed.

## Authentication

Register or log in through the account endpoints, then send the access token on protected requests:

```text
Authorization: Bearer <access-token>
```

Supported registration roles are `Admin`, `Instructor`, and `Student`. Login and refresh requests are rate-limited to five requests per IP address per minute and may return `429 Too Many Requests`.

### Account endpoints

| Method | Route                   | Body                | Description                                                      |
| ------ | ----------------------- | ------------------- | ---------------------------------------------------------------- |
| `POST` | `/api/Account/register` | `RegisterDto`       | Register a user.                                                 |
| `POST` | `/api/Account/login`    | `LoginRequestDto`   | Log in with username or email and receive access/refresh tokens. |
| `POST` | `/api/Account/refresh`  | `RefreshRequestDto` | Validate and rotate a refresh token.                             |
| `POST` | `/api/Account/logout`   | `LogoutRequestDto`  | Revoke a refresh token.                                          |

## API endpoints

All non-account endpoints require a valid JWT. Role requirements are listed below.

### Courses — `/api/Courses`

| Method                     | Roles                      | Description                                                     |
| -------------------------- | -------------------------- | --------------------------------------------------------------- |
| `GET /api/Courses`         | Admin, Instructor, Student | List courses.                                                   |
| `GET /api/Courses/{id}`    | Admin, Instructor, Student | Get one course.                                                 |
| `POST /api/Courses`        | Admin, Instructor          | Create a course using `CourseCreateDto`.                        |
| `PUT /api/Courses/{id}`    | Admin, Instructor          | Update a course; instructors can update only their own courses. |
| `DELETE /api/Courses/{id}` | Admin, Instructor          | Delete a course; instructors can delete only their own courses. |

New courses are created with `Draft` status and a server-generated creation date.

### Enrollments — `/api/Enrollments`

| Method                         | Roles                      | Description                                                 |
| ------------------------------ | -------------------------- | ----------------------------------------------------------- |
| `GET /api/Enrollments`         | Admin                      | List all enrollments.                                       |
| `GET /api/Enrollments/{id}`    | Admin, Instructor, Student | Get an enrollment when the caller is authorized to view it. |
| `POST /api/Enrollments`        | Admin, Student             | Create an enrollment using `EnrollmentCreateDto`.           |
| `PUT /api/Enrollments/{id}`    | Admin, Instructor          | Update an enrollment.                                       |
| `DELETE /api/Enrollments/{id}` | Admin, Instructor          | Delete an enrollment.                                       |

Students are restricted to enrolling themselves. New enrollments start with `Active` status.

### Instructors — `/api/Instructors`

| Method                         | Roles                      | Description                                                   |
| ------------------------------ | -------------------------- | ------------------------------------------------------------- |
| `GET /api/Instructors`         | Admin, Instructor, Student | List instructors.                                             |
| `GET /api/Instructors/{id}`    | Admin, Instructor, Student | Get one instructor.                                           |
| `POST /api/Instructors`        | Admin                      | Create an instructor using `InstructorCreateDto`.             |
| `PUT /api/Instructors/{id}`    | Admin, Instructor          | Update an instructor; instructors can update only themselves. |
| `DELETE /api/Instructors/{id}` | Admin                      | Delete an instructor.                                         |

### Students — `/api/Students`

| Method                              | Roles                      | Description                                                 |
| ----------------------------------- | -------------------------- | ----------------------------------------------------------- |
| `GET /api/Students`                 | Admin, Instructor          | List students.                                              |
| `GET /api/Students/{id}`            | Admin, Instructor, Student | Get a student; students can access only themselves.         |
| `POST /api/Students`                | Admin                      | Create a student using `StudentCreateDto`.                  |
| `PUT /api/Students/{id}`            | Admin, Student             | Update a student; students can update only themselves.      |
| `DELETE /api/Students/{id}`         | Admin                      | Delete a student.                                           |
| `GET /api/Students/{id}/profile`    | Admin, Student             | Get a student's profile.                                    |
| `PUT /api/Students/{id}/profile`    | Admin, Student             | Create or update a profile using `StudentProfileCreateDto`. |
| `DELETE /api/Students/{id}/profile` | Admin, Student             | Delete a student's profile.                                 |

## Data model

- A `Student` can have many `Enrollment` records and one optional `StudentProfile`.
- An `Instructor` can teach many `Course` records and can have manager/subordinate relationships with other instructors.
- A `Course` can have many enrollments.
- A `User` can optionally be linked to a student or instructor record.
- Unique constraints protect student email, instructor email, username, user email, course code, and duplicate student/course enrollments.

DTOs are organized under `DTOs/Auth`, `DTOs/Course`, `DTOs/Enrollment`, `DTOs/Instructor`, `DTOs/Student`, and `DTOs/StudentProfile`.

## Project structure

```text
Controllers/    HTTP API endpoints
Data/           EF Core DbContext
DTOs/           Request and response models
Entities/       Database entities
Extensions/     Service registration and middleware extensions
Profiles/       AutoMapper configuration
Repositories/   Generic repository and Unit of Work implementations
Migrations/     EF Core migrations
```

## Development notes

- Swagger is enabled only in the Development environment.
- CORS allows `https://localhost:7123` and `http://localhost:3000`.
- Global exception handling and security audit logging are registered in the middleware pipeline.
- No automated test project is currently included. Run `dotnet build` to verify compilation.
