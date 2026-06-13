# GlycemicTracker

GlycemicTracker is a premium, high-visual-aesthetic dark mode web application designed to track food consumption, estimate and visualize blood glucose response curves using a mathematical pharmacokinetic absorption model, and record actual finger-prick measurements.

Developed as a modern personal health assistant, the application provides a stunning glassmorphic UI with dynamic charting to help users understand how different foods (including those containing alcohol) affect their blood glucose levels over time.

---

## 🌟 Key Features

### 1. Pharmacokinetic Glucose Response Simulation
* **Physiological Modeling**: Models blood glucose absorption curves based on food **Glycemic Index (GI)** and **Glycemic Load (GL)**.
* **Macronutrient Adjustments**: Automatically adjusts time-to-peak and flattens curves based on **Fiber**, **Protein**, and **Fat** content to model realistic digestion.
* **Meal Overlaps**: Integrates overlapping glucose curves at 5-minute increments for continuous, realistic simulation.

### 2. Hepatic Suppression & Alcohol Modeling
* **Alcohol Kinetics**: Models the physiological impact of alcohol consumption:
  * *Hepatic Suppression*: Temporarily subtracts baseline glucose production (up to `1.2 mmol/L`), decaying linearly over a duration determined by alcohol units.
  * *Extended Clearance*: Slows carbohydrate clearance rate relative to alcohol units in the bloodstream.
  * *Hypoglycemia Protection*: Clamps estimated glucose at a safe floor of `3.0 mmol/L`.
* **Zero-Order Clearance**: Uses a realistic clearance multiplier (`0.80`) matching typical body elimination.

### 3. Glassmorphic UI & Advanced Charting
* **Neon Glow Dashboard**: Implements custom CSS with neon borders, custom tooltips, glassmorphism card layouts, and subtle animations.
* **Scriptable Gradients**: The Chart.js curve dynamically transitions color:
  * **Cyan/Blue** for normal values (`< 7.8 mmol/L`).
  * **Red** for spikes (`>= 7.8 mmol/L`).
* **Interactive Elements**:
  * Glowing dashed horizontal **Spike Limit** indicator.
  * Vertical amber dashed **NOW** timeline showing the current position on the curve.
  * Actual glucose measurements plotted as scatter overlays for visual calibration.

### 4. PDF Report Export
* **TinyPdf Integration**: Generates professional PDF health summaries aggregating the last 7 days of food logs, statistics (average glucose, peak, TIR%, total carbs, average GI, and spike counts), and reports formatted for sharing with a doctor.

---

## 🛠️ Technology Stack

* **Backend**: ASP.NET Core (.NET 10)
* **Data Access**: Raw ADO.NET (`Microsoft.Data.SqlClient`) for lightweight, high-performance parameterized SQL queries (no Entity Framework / Dapper).
* **Database**: Microsoft SQL Server
* **Frontend**: HTML5, Vanilla CSS, Bootstrap 5, jQuery (with jQuery UI Autocomplete), and Chart.js.
* **PDF Engine**: TinyPdf (.NET port of the minimal PDF library)

---

## 📁 Repository Structure

```
/
├── GlycemicTracker.sln         # .NET Solution File
├── README.md                   # Documentation
├── LICENSE                     # MIT License
├── .gitignore                  # Git Ignore configuration
└── src/
    └── GlycemicTracker/        # Main ASP.NET Core Project
        ├── Controllers/        # Home, Logs, and Foods Controllers
        ├── Data/               # DB Helper, Initializer, and Repositories
        ├── Models/             # Food, Log, and Reading Schemas
        ├── Services/           # Pharmacokinetic Curve Calculator
        ├── Views/              # Razor Pages and Layout templates
        ├── wwwroot/            # CSS, JS, Favicons, and Third-Party Libs
        ├── Program.cs          # Web host initialization
        └── appsettings.json    # Application configuration (placeholders)
```

---

## 🚀 Local Setup & Installation

### Prerequisites
* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* [Microsoft SQL Server](https://www.microsoft.com/sql-server/) (or LocalDB)

### 1. Database Setup
The application dynamically initializes and seeds its own database on startup based on the connection string configured. Around 150 standard foods are pre-loaded into the database.

### 2. Configure Connection String (User Secrets)
To keep sensitive database credentials secure, initialize and configure User Secrets in the project:

```bash
# Initialize User Secrets
dotnet user-secrets init --project src/GlycemicTracker/GlycemicTracker.csproj

# Set your connection string (replace credentials as needed)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=YOUR_SERVER;Database=CarbTracker;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true;" --project src/GlycemicTracker/GlycemicTracker.csproj
```

### 3. Build & Run
Run the application using the dotnet CLI from the root folder:

```bash
# Restore dependencies and build
dotnet build

# Run the web server
dotnet run --project src/GlycemicTracker/GlycemicTracker.csproj
```
The application will start listening on `http://localhost:5014`. Open your browser and navigate to this URL to view the Glycemic Controller dashboard.

---

## 📄 License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
