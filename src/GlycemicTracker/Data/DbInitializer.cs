using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace GlycemicTracker.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(string connectionString)
        {
            // 1. Create the database if it doesn't exist.
            var builder = new SqlConnectionStringBuilder(connectionString);
            var dbName = builder.InitialCatalog;
            if (string.IsNullOrEmpty(dbName))
            {
                dbName = "GlycemicTracker"; // fallback
            }

            // Connect to master database to check/create the target database
            builder.InitialCatalog = "master";
            var masterConnectionString = builder.ConnectionString;

            using (var masterConnection = new SqlConnection(masterConnectionString))
            {
                await masterConnection.OpenAsync();
                
                // Check if target database exists
                var checkDbSql = $"SELECT database_id FROM sys.databases WHERE name = '{dbName}'";
                using (var checkCommand = new SqlCommand(checkDbSql, masterConnection))
                {
                    var result = await checkCommand.ExecuteScalarAsync();
                    if (result == null)
                    {
                        // Create Database
                        var createDbSql = $"CREATE DATABASE {dbName}";
                        using (var createCommand = new SqlCommand(createDbSql, masterConnection))
                        {
                            await createCommand.ExecuteNonQueryAsync();
                        }
                        // Give database engine a moment to provision the files
                        await Task.Delay(2000);
                    }
                }
            }

            // 2. Create tables in the GlycemicTracker database
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Create Foods Table
                var createFoodsTableSql = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Foods')
                    BEGIN
                        CREATE TABLE Foods (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Name NVARCHAR(150) NOT NULL UNIQUE,
                            GlycemicIndex INT NOT NULL,
                            CarbsPer100g DECIMAL(5,2) NOT NULL,
                            SugarPer100g DECIMAL(5,2) NOT NULL,
                            FiberPer100g DECIMAL(5,2) NOT NULL,
                            ProteinPer100g DECIMAL(5,2) NOT NULL,
                            FatPer100g DECIMAL(5,2) NOT NULL,
                            CaloriesPer100g INT NOT NULL,
                            IsCustom BIT NOT NULL DEFAULT 0,
                            AlcoholGrams DECIMAL(5,2) NOT NULL DEFAULT 0.0
                        );
                        CREATE INDEX IX_Foods_Name ON Foods(Name);
                    END";
                using (var command = new SqlCommand(createFoodsTableSql, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }

                // Create FoodLogs Table
                var createFoodLogsTableSql = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FoodLogs')
                    BEGIN
                        CREATE TABLE FoodLogs (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            FoodId INT NOT NULL FOREIGN KEY REFERENCES Foods(Id) ON DELETE CASCADE,
                            AmountGrams DECIMAL(6,2) NOT NULL,
                            LogTime DATETIME2 NOT NULL,
                            CarbsGrams DECIMAL(6,2) NOT NULL,
                            SugarGrams DECIMAL(6,2) NOT NULL,
                            FiberGrams DECIMAL(6,2) NOT NULL,
                            ProteinGrams DECIMAL(6,2) NOT NULL,
                            FatGrams DECIMAL(6,2) NOT NULL,
                            GlycemicLoad DECIMAL(5,2) NOT NULL,
                            AlcoholGrams DECIMAL(6,2) NOT NULL DEFAULT 0.0
                        );
                        CREATE INDEX IX_FoodLogs_LogTime ON FoodLogs(LogTime);
                    END";
                using (var command = new SqlCommand(createFoodLogsTableSql, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }

                // Create GlucoseReadings Table
                var createGlucoseReadingsTableSql = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GlucoseReadings')
                    BEGIN
                        CREATE TABLE GlucoseReadings (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            ReadingTime DATETIME2 NOT NULL,
                            GlucoseValue DECIMAL(4,1) NOT NULL,
                            Notes NVARCHAR(250) NULL
                        );
                        CREATE INDEX IX_GlucoseReadings_ReadingTime ON GlucoseReadings(ReadingTime);
                    END";
                using (var command = new SqlCommand(createGlucoseReadingsTableSql, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }

                // Create GlycemicParameters Table
                var createGlycemicParametersTableSql = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GlycemicParameters')
                    BEGIN
                        CREATE TABLE GlycemicParameters (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            StartDate DATE NOT NULL UNIQUE,
                            BaselineGlucose DECIMAL(4,1) NOT NULL DEFAULT 5.0,
                            InsulinResistanceFactor DECIMAL(5,3) NOT NULL DEFAULT 0.160,
                            DawnAmplitude DECIMAL(4,1) NOT NULL DEFAULT 3.6
                        );
                        CREATE INDEX IX_GlycemicParameters_StartDate ON GlycemicParameters(StartDate);
                    END";
                using (var command = new SqlCommand(createGlycemicParametersTableSql, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }

                // Seed default glycemic parameters if table is empty
                var seedParametersSql = @"
                    IF (SELECT COUNT(*) FROM GlycemicParameters) = 0
                    BEGIN
                        INSERT INTO GlycemicParameters (StartDate, BaselineGlucose, InsulinResistanceFactor, DawnAmplitude)
                        VALUES ('2026-01-01', 5.0, 0.160, 3.6);
                    END";
                using (var command = new SqlCommand(seedParametersSql, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }

                // Schema Migration: Add AlcoholGrams column if missing from existing database tables
                var migrateSchemaSql = @"
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Foods') AND name = 'AlcoholGrams')
                    BEGIN
                        ALTER TABLE Foods ADD AlcoholGrams DECIMAL(5,2) NOT NULL DEFAULT 0.0;
                    END;
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FoodLogs') AND name = 'AlcoholGrams')
                    BEGIN
                        ALTER TABLE FoodLogs ADD AlcoholGrams DECIMAL(6,2) NOT NULL DEFAULT 0.0;
                    END;";
                using (var command = new SqlCommand(migrateSchemaSql, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }

                // 3. Seed and synchronize foods
                await SeedFoodsAsync(connection);
            }
        }

        private static async Task SeedFoodsAsync(SqlConnection connection)
        {
            var seedFoods = new List<(string Name, int GI, double Carbs, double Sugar, double Fiber, double Protein, double Fat, int Calories)>
            {
                // Grains, Breads, and Pasta
                ("White Bread", 75, 49.0, 5.0, 2.3, 9.0, 3.2, 265),
                ("Whole Wheat Bread", 51, 41.0, 4.0, 7.0, 12.0, 3.5, 247),
                ("White Rice (Cooked)", 73, 28.0, 0.1, 0.4, 2.7, 0.3, 130),
                ("Brown Rice (Cooked)", 50, 23.0, 0.4, 1.8, 2.6, 0.9, 111),
                ("Oatmeal / Rolled Oats (Cooked)", 55, 12.0, 0.5, 1.7, 2.5, 1.4, 71),
                ("Spaghetti / Pasta (White, Cooked)", 49, 31.0, 1.5, 1.8, 5.8, 0.9, 158),
                ("Whole Wheat Pasta (Cooked)", 42, 26.5, 0.8, 3.9, 5.3, 0.8, 124),
                ("Quinoa (Cooked)", 53, 21.3, 0.9, 2.8, 4.4, 1.9, 120),
                ("Cornflakes Cereal", 81, 84.0, 9.3, 3.0, 7.0, 0.4, 357),
                ("Bagel (Plain)", 72, 48.0, 6.0, 2.2, 10.0, 1.5, 250),
                ("Croissant", 67, 46.0, 11.0, 2.6, 8.2, 21.0, 406),
                ("Naan Bread", 71, 50.0, 4.0, 2.5, 9.0, 5.0, 290),
                ("Muesli Cereal", 57, 64.0, 20.0, 8.0, 10.0, 5.5, 365),
                ("Rye Bread", 58, 48.0, 3.8, 5.8, 8.5, 3.3, 259),
                
                // Starchy Vegetables
                ("Potato (Boiled)", 78, 20.0, 0.8, 1.8, 1.9, 0.1, 87),
                ("Potato (Mashed with butter/milk)", 82, 16.0, 1.2, 1.5, 1.8, 4.2, 108),
                ("French Fries (Baked)", 75, 35.0, 0.5, 3.0, 3.4, 15.0, 289),
                ("Sweet Potato (Boiled)", 63, 18.0, 4.2, 3.0, 1.6, 0.1, 76),
                ("Sweet Potato (Baked)", 94, 21.0, 6.5, 3.3, 2.0, 0.2, 90),
                ("Sweet Corn (Cooked)", 55, 19.0, 6.3, 2.0, 3.2, 1.2, 96),
                
                // Non-Starchy Vegetables
                ("Broccoli (Boiled)", 15, 7.0, 1.7, 2.6, 2.8, 0.4, 35),
                ("Carrots (Raw)", 35, 9.6, 4.7, 2.8, 0.9, 0.2, 41),
                ("Carrots (Boiled)", 39, 8.0, 3.5, 3.0, 0.8, 0.2, 35),
                ("Spinach (Raw)", 15, 3.6, 0.4, 2.2, 2.9, 0.4, 23),
                ("Tomatoes (Raw)", 15, 3.9, 2.6, 1.2, 0.9, 0.2, 18),
                ("Cucumber (Raw)", 15, 3.6, 1.7, 0.5, 0.7, 0.1, 15),
                ("Bell Pepper (Raw)", 15, 6.0, 4.2, 2.1, 1.0, 0.3, 31),
                ("Onions (Raw)", 15, 9.3, 4.2, 1.7, 1.1, 0.1, 40),
                ("Cauliflower (Cooked)", 15, 4.1, 1.9, 2.0, 1.9, 0.3, 23),
                ("Green Peas (Boiled)", 51, 14.4, 5.7, 5.1, 5.4, 0.4, 84),
                ("Mushrooms (Raw)", 15, 3.3, 2.0, 1.0, 3.1, 0.3, 22),
                ("Lettuce (Raw)", 15, 2.9, 0.8, 1.3, 1.4, 0.2, 15),
                
                // Fruits
                ("Apple (Raw, with skin)", 39, 14.0, 10.0, 2.4, 0.3, 0.2, 52),
                ("Banana (Ripe)", 51, 23.0, 12.0, 2.6, 1.1, 0.3, 89),
                ("Banana (Overripe/Brown)", 60, 24.0, 15.0, 2.2, 1.1, 0.3, 95),
                ("Orange (Raw)", 43, 11.8, 9.4, 2.4, 0.9, 0.1, 47),
                ("Grapes (Red/Green)", 59, 18.0, 15.5, 0.9, 0.7, 0.2, 69),
                ("Strawberries (Raw)", 40, 7.7, 4.9, 2.0, 0.7, 0.3, 32),
                ("Blueberries (Raw)", 53, 14.5, 10.0, 2.4, 0.7, 0.3, 57),
                ("Watermelon (Raw)", 72, 7.6, 6.2, 0.4, 0.6, 0.2, 30),
                ("Peach (Raw)", 42, 9.5, 8.4, 1.5, 0.9, 0.3, 39),
                ("Pear (Raw)", 38, 15.0, 9.8, 3.1, 0.4, 0.1, 57),
                ("Pineapple (Raw)", 59, 13.0, 10.0, 1.4, 0.5, 0.1, 50),
                ("Mango (Raw)", 51, 15.0, 13.7, 1.6, 0.8, 0.4, 60),
                ("Grapefruit (Raw)", 25, 8.0, 7.0, 1.1, 0.6, 0.1, 32),
                ("Cherries (Sweet, Raw)", 22, 16.0, 12.8, 2.1, 1.1, 0.2, 63),
                ("Dates (Dried)", 62, 75.0, 66.0, 8.0, 2.5, 0.4, 282),
                ("Raisins", 64, 79.0, 59.0, 3.7, 3.1, 0.5, 299),
                
                // Proteins (GI = 0)
                ("Chicken Breast (Grilled)", 0, 0.0, 0.0, 0.0, 31.0, 3.6, 165),
                ("Chicken Thigh (Baked)", 0, 0.0, 0.0, 0.0, 24.0, 9.0, 177),
                ("Beef Steak (Grilled)", 0, 0.0, 0.0, 0.0, 26.0, 15.0, 250),
                ("Ground Beef (15% Fat, Cooked)", 0, 0.0, 0.0, 0.0, 24.0, 17.0, 252),
                ("Pork Chop (Grilled)", 0, 0.0, 0.0, 0.0, 27.0, 12.0, 220),
                ("Tesco Finest Signature Pork Sausages", 0, 2.9, 0.4, 1.0, 15.9, 26.7, 317),
                ("Bacon (Crispy)", 0, 1.4, 0.0, 0.0, 37.0, 42.0, 541),
                ("Salmon Fillet (Baked)", 0, 0.0, 0.0, 0.0, 22.0, 13.0, 206),
                ("Tuna (Canned in water)", 0, 0.0, 0.0, 0.0, 26.0, 1.0, 116),
                ("Cod Fillet (Baked)", 0, 0.0, 0.0, 0.0, 18.0, 0.7, 82),
                ("Shrimp (Cooked)", 0, 0.2, 0.0, 0.0, 24.0, 0.3, 99),
                ("Egg (Large, Hard Boiled)", 0, 0.6, 0.6, 0.0, 12.6, 10.6, 155),
                ("Tofu (Firm)", 15, 1.9, 0.0, 0.9, 8.0, 4.8, 76),
                
                // Dairy & Dairy Alternatives
                ("Whole Milk", 31, 4.8, 4.8, 0.0, 3.2, 3.2, 61),
                ("Semi-Skimmed Milk", 27, 4.8, 4.8, 0.0, 3.4, 1.7, 50),
                ("Skimmed Milk", 32, 5.0, 5.0, 0.0, 3.4, 0.1, 35),
                ("Greek Yogurt (Plain, Full Fat)", 12, 3.5, 3.5, 0.0, 9.0, 5.0, 97),
                ("Low Fat Yogurt (Fruit flavored)", 36, 19.0, 17.0, 0.1, 4.4, 1.2, 105),
                ("Cheddar Cheese", 0, 1.3, 0.5, 0.0, 25.0, 33.0, 403),
                ("Mozzarella Cheese", 0, 2.2, 1.0, 0.0, 22.0, 22.0, 300),
                ("Soy Milk (Unsweetened)", 30, 1.6, 0.5, 0.5, 3.3, 1.8, 33),
                ("Almond Milk (Unsweetened)", 25, 0.6, 0.0, 0.2, 0.4, 1.1, 15),
                ("Butter", 0, 0.1, 0.1, 0.0, 0.9, 81.0, 717),
                ("Heavy Cream", 0, 2.7, 2.7, 0.0, 2.1, 36.0, 340),
                
                // Fats and Oils (GI = 0)
                ("Olive Oil", 0, 0.0, 0.0, 0.0, 0.0, 100.0, 884),
                ("Coconut Oil", 0, 0.0, 0.0, 0.0, 0.0, 100.0, 862),
                ("Canola / Vegetable Oil", 0, 0.0, 0.0, 0.0, 0.0, 100.0, 884),
                ("Mayonnaise", 0, 0.6, 0.6, 0.0, 1.0, 75.0, 680),
                
                // Legumes, Nuts and Seeds
                ("Lentils (Cooked)", 32, 20.0, 1.8, 7.9, 9.0, 0.4, 116),
                ("Chickpeas (Cooked)", 28, 27.0, 4.8, 7.6, 8.9, 2.6, 164),
                ("Black Beans (Cooked)", 30, 22.8, 0.3, 8.7, 8.9, 0.5, 132),
                ("Peanuts (Raw)", 14, 16.0, 4.7, 8.5, 26.0, 49.0, 567),
                ("Peanut Butter (Smooth)", 14, 20.0, 9.2, 6.0, 25.0, 50.0, 588),
                ("Almonds", 15, 22.0, 4.3, 12.0, 21.0, 49.0, 579),
                ("Walnuts", 15, 13.7, 2.6, 6.7, 15.2, 65.2, 654),
                ("Cashews", 25, 30.0, 5.9, 3.3, 18.0, 44.0, 553),
                ("Tesco Roasted Mixed Nuts", 15, 10.7, 3.9, 8.0, 20.4, 55.6, 641),
                ("Chia Seeds", 15, 42.0, 0.0, 34.0, 17.0, 31.0, 486),
                ("Pumpkin Seeds / Pepitas", 15, 10.7, 1.4, 6.0, 30.0, 49.0, 559),
                
                // Sweeteners & Condiments
                ("White Sugar", 65, 100.0, 100.0, 0.0, 0.0, 0.0, 387),
                ("Honey", 58, 82.0, 82.0, 0.0, 0.3, 0.0, 304),
                ("Maple Syrup", 54, 67.0, 60.0, 0.0, 0.0, 0.1, 260),
                ("Tomato Ketchup", 55, 26.0, 22.0, 0.3, 1.2, 0.1, 112),
                
                // Beverages
                ("Coca-Cola Classic", 63, 10.6, 10.6, 0.0, 0.0, 0.0, 42),
                ("Diet Soda (Cola)", 0, 0.0, 0.0, 0.0, 0.0, 0.0, 0),
                ("Orange Juice (100% Pure)", 50, 10.4, 8.4, 0.2, 0.7, 0.2, 45),
                ("Apple Juice (100% Pure)", 41, 11.3, 9.6, 0.2, 0.1, 0.1, 46),
                ("Beer (Regular)", 89, 3.6, 0.0, 0.0, 0.5, 0.0, 43),
                ("Dortmunder Vier", 89, 2.5, 0.1, 0.0, 0.4, 0.0, 37),
                ("Red Wine", 0, 2.6, 0.6, 0.0, 0.1, 0.0, 85),
                ("Coffee (Black, No sugar)", 0, 0.0, 0.0, 0.0, 0.1, 0.0, 1),
                ("Black Tea (No milk/sugar)", 0, 0.2, 0.0, 0.0, 0.0, 0.0, 1),
                
                // Snacks & Fast Foods
                ("Milk Chocolate Bar", 49, 59.0, 52.0, 3.4, 7.6, 30.0, 535),
                ("Dark Chocolate (70% Cocoa)", 23, 46.0, 29.0, 11.0, 7.8, 43.0, 598),
                ("Potato Chips (Salted)", 60, 53.0, 1.3, 4.4, 6.5, 35.0, 536),
                ("Popcorn (Air-Popped)", 55, 78.0, 0.9, 14.5, 12.0, 4.2, 387),
                ("Pizza Margherita", 60, 30.0, 3.0, 2.2, 11.0, 10.0, 266),
                ("Chocolate Chip Cookie", 64, 62.0, 35.0, 2.0, 5.0, 24.0, 488),
                ("Vanilla Ice Cream", 57, 24.0, 21.0, 0.7, 3.5, 11.0, 207),
                ("Hamburger (Fast Food)", 65, 30.0, 6.0, 1.8, 14.0, 10.0, 265),
                ("Glazed Donut", 76, 51.0, 27.0, 1.5, 4.9, 19.0, 398),
                ("Corn Tortilla Chips", 72, 65.0, 1.1, 6.0, 7.0, 23.0, 489),
                ("Salted Pretzels", 83, 80.0, 2.2, 3.0, 10.0, 2.6, 380),
                
                // Custom User Tesco Foods
                ("Tesco Unsmoked Back Bacon Rashers", 0, 0.5, 0.5, 0.5, 17.4, 13.2, 191),
                ("Tesco Finest White Loaf", 75, 45.5, 4.6, 2.9, 8.0, 1.1, 230),
                ("Tesco Finest Jersey Royals", 62, 14.9, 1.1, 1.8, 1.8, 0.1, 71),
                ("Tesco British Crumbed Ham Slices", 0, 2.1, 1.0, 0.1, 21.2, 2.0, 112),
                ("Tesco Mature Cheddar", 0, 0.1, 0.1, 0.0, 25.4, 34.9, 416),
                ("Tesco Sweet Pickled Silverskin Onions", 35, 7.6, 4.7, 1.9, 0.5, 0.3, 39),
                ("Tesco Pitted Green Olives", 15, 2.0, 2.0, 2.5, 1.4, 21.0, 208),
                ("Tesco Caesar Dressing", 15, 6.6, 3.9, 0.5, 1.5, 44.0, 429),
                ("Tesco Finest Cooked Jumbo King Prawns", 0, 0.4, 0.4, 0.0, 18.4, 0.5, 80),
                ("Tesco Italian Chopped Tomatoes", 35, 4.0, 4.0, 0.9, 1.4, 0.2, 25),
                ("Tesco Lean Pork Mince 5% Fat", 0, 0.2, 0.2, 0.0, 20.5, 4.8, 126),
                ("Tesco Ground Almonds", 15, 6.9, 4.2, 9.1, 21.1, 55.8, 632),
                ("Baking Powder", 70, 33.6, 0.3, 2.9, 3.5, 0.7, 161)
            };

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // 1. Create temporary table
                    var createTempTableSql = @"
                        CREATE TABLE #TempFoods (
                            Name NVARCHAR(150) COLLATE Database_Default,
                            GlycemicIndex INT,
                            CarbsPer100g DECIMAL(5,2),
                            SugarPer100g DECIMAL(5,2),
                            FiberPer100g DECIMAL(5,2),
                            ProteinPer100g DECIMAL(5,2),
                            FatPer100g DECIMAL(5,2),
                            CaloriesPer100g INT,
                            IsCustom BIT,
                            AlcoholGrams DECIMAL(5,2)
                        );";
                    using (var createTempCmd = new SqlCommand(createTempTableSql, connection, transaction))
                    {
                        await createTempCmd.ExecuteNonQueryAsync();
                    }

                    // 2. Populate DataTable
                    var dt = new DataTable();
                    dt.Columns.Add("Name", typeof(string));
                    dt.Columns.Add("GlycemicIndex", typeof(int));
                    dt.Columns.Add("CarbsPer100g", typeof(decimal));
                    dt.Columns.Add("SugarPer100g", typeof(decimal));
                    dt.Columns.Add("FiberPer100g", typeof(decimal));
                    dt.Columns.Add("ProteinPer100g", typeof(decimal));
                    dt.Columns.Add("FatPer100g", typeof(decimal));
                    dt.Columns.Add("CaloriesPer100g", typeof(int));
                    dt.Columns.Add("IsCustom", typeof(bool));
                    dt.Columns.Add("AlcoholGrams", typeof(decimal));

                    foreach (var food in seedFoods)
                    {
                        double alcohol = 0.0;
                        if (food.Name == "Dortmunder Vier") alcohol = 3.2;
                        else if (food.Name == "Beer (Regular)") alcohol = 3.6;
                        else if (food.Name == "Red Wine") alcohol = 10.0;

                        dt.Rows.Add(
                            food.Name,
                            food.GI,
                            (decimal)food.Carbs,
                            (decimal)food.Sugar,
                            (decimal)food.Fiber,
                            (decimal)food.Protein,
                            (decimal)food.Fat,
                            food.Calories,
                            false,
                            (decimal)alcohol
                        );
                    }

                    // 3. Bulk Copy to Temp Table (batch size 5000 as requested)
                    using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
                    {
                        bulkCopy.DestinationTableName = "#TempFoods";
                        bulkCopy.BatchSize = 5000;
                        await bulkCopy.WriteToServerAsync(dt);
                    }

                    // 4. Merge Temp Table into Target Table
                    var mergeSql = @"
                        MERGE INTO Foods AS Target
                        USING #TempFoods AS Source
                        ON Target.Name = Source.Name
                        WHEN MATCHED THEN
                            UPDATE SET 
                                GlycemicIndex = Source.GlycemicIndex,
                                CarbsPer100g = Source.CarbsPer100g,
                                SugarPer100g = Source.SugarPer100g,
                                FiberPer100g = Source.FiberPer100g,
                                ProteinPer100g = Source.ProteinPer100g,
                                FatPer100g = Source.FatPer100g,
                                CaloriesPer100g = Source.CaloriesPer100g,
                                AlcoholGrams = Source.AlcoholGrams
                        WHEN NOT MATCHED THEN
                            INSERT (Name, GlycemicIndex, CarbsPer100g, SugarPer100g, FiberPer100g, ProteinPer100g, FatPer100g, CaloriesPer100g, IsCustom, AlcoholGrams)
                            VALUES (Source.Name, Source.GlycemicIndex, Source.CarbsPer100g, Source.SugarPer100g, Source.FiberPer100g, Source.ProteinPer100g, Source.FatPer100g, Source.CaloriesPer100g, Source.IsCustom, Source.AlcoholGrams);";

                    using (var mergeCmd = new SqlCommand(mergeSql, connection, transaction))
                    {
                        await mergeCmd.ExecuteNonQueryAsync();
                    }

                    // 5. Clean up temporary table
                    using (var dropTempCmd = new SqlCommand("DROP TABLE #TempFoods", connection, transaction))
                    {
                        await dropTempCmd.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
    }
}
