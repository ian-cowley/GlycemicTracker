# Contributing to GlycemicTracker

Thank you for your interest in contributing to GlycemicTracker! We welcome community contributions to help improve this glycemic response simulator.

---

## 🙋 Getting Started

1. **Fork the Repository**: Create a personal fork of the repository on GitHub.
2. **Clone the Project**: Clone your fork locally to your development machine.
3. **Set Up Development Environment**:
   - Ensure you have the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed.
   - Set up your local database and configure your connection strings via **.NET User Secrets** (see setup instructions in the [README](README.md)).

---

## 🐛 Reporting Bugs

If you find a bug:
1. **Search Existing Issues**: Check if the bug has already been reported.
2. **Open a New Issue**: Use the **Bug Report** template to describe the issue:
   - Provide a clear, descriptive title.
   - Describe the steps to reproduce the behavior.
   - Explain what you expected to happen vs. what actually happened.
   - Include any screenshots or console logs if applicable.

---

## 💡 Suggesting Enhancements

To propose a new feature or improvement:
1. Open an issue using the **Feature Request** template.
2. Explain the goal, motivation, and potential implementation path.
3. Participate in the community discussion to refine the design.

---

## 🔧 Submitting Pull Requests (PRs)

When you are ready to submit code changes:

### 1. Create a Branch
Create a descriptive feature branch:
```bash
git checkout -b feature/your-feature-name
# or
git checkout -b bugfix/your-bugfix-name
```

### 2. Code Standards
* Follow standard C# and ASP.NET Core formatting guidelines.
* Keep controllers lightweight and place math/physiological business logic inside the `Services/` layer.
* Ensure all database operations in the `Data/` layer use raw, parameterized ADO.NET queries to prevent SQL injection.
* Make sure no credentials, server IPs, or passwords are committed to source files. Keep secrets in local User Secrets.

### 3. Build & Test Locally
Before committing, verify that the project compiles cleanly:
```bash
# Verify project build
dotnet build

# Verify project publish targets
dotnet publish src/GlycemicTracker/GlycemicTracker.csproj -c Release -o publish
```

### 4. Create your PR
* Push your branch to your GitHub fork.
* Open a Pull Request from your fork back to the `main` branch of the official repository.
* Fill out the Pull Request template completely.
