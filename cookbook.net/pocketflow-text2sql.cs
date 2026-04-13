#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj
#:package Microsoft.Data.Sqlite

using PocketFlow;
using Microsoft.Data.Sqlite;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class GetSchemaNode : Node<TShared, string, string>
{
    public override Task<string?> Prep(TShared shared)
    {
        var dbPath = shared.TryGetValue("db_path", out var db) ? db as string ?? "ecommerce.db" : "ecommerce.db";
        return Task.FromResult<string?>(dbPath);
    }

    public override Task<string> Exec(string dbPath)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        
        var schema = new List<string>();
        
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
        using var reader = cmd.ExecuteReader();
        
        while (reader.Read())
        {
            var tableName = reader.GetString(0);
            schema.Add($"Table: {tableName}");
            
            using var innerCmd = conn.CreateCommand();
            innerCmd.CommandText = $"PRAGMA table_info({tableName});";
            using var innerReader = innerCmd.ExecuteReader();
            
            while (innerReader.Read())
            {
                var colName = innerReader.GetString(1);
                var colType = innerReader.GetString(2);
                schema.Add($"  - {colName} ({colType})");
            }
            schema.Add("");
        }
        
        return Task.FromResult(string.Join("\n", schema).Trim());
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string execRes)
    {
        shared["schema"] = execRes;
        Console.WriteLine("\n===== DB SCHEMA =====\n");
        Console.WriteLine(execRes);
        Console.WriteLine("\n=====================\n");
        return Task.FromResult<string?>("default");
    }
}

class GenerateSQLNode : Node<TShared, (string Query, string Schema), string>
{
    public override Task<(string Query, string Schema)?> Prep(TShared shared)
    {
        var query = shared.TryGetValue("natural_query", out var q) ? q as string ?? "" : "";
        var schema = shared.TryGetValue("schema", out var s) ? s as string ?? "" : "";
        return Task.FromResult<(string, string)?>((query, schema));
    }

    public override Task<string> Exec((string Query, string Schema) prepRes)
    {
        var (query, schema) = prepRes;
        
        var prompt = $@"Given SQLite schema:
{schema}

Question: ""{query}""

Respond ONLY with a YAML block containing the SQL query under the key 'sql':
```yaml
sql: |
  SELECT ...
```";

        Console.WriteLine("[GenerateSQL] Generating SQL query...");
        var response = MockCallLLM(prompt);
        
        var yamlStr = response.Split("```yaml")[1].Split("```")[0].Trim();
        var sqlQuery = ExtractSqlFromYaml(yamlStr);
        
        return Task.FromResult(sqlQuery);
    }

    public override Task<string?> Post(TShared shared, (string Query, string Schema)? prepRes, string execRes)
    {
        shared["generated_sql"] = execRes;
        shared["debug_attempts"] = 0;
        
        Console.WriteLine($"\n===== GENERATED SQL (Attempt {(shared.TryGetValue("debug_attempts", out var d) ? (int)d : 0) + 1}) =====\n");
        Console.WriteLine(execRes);
        Console.WriteLine("\n====================================\n");
        
        return Task.FromResult<string?>("default");
    }

    private static string ExtractSqlFromYaml(string yaml)
    {
        var lines = yaml.Split('\n');
        var sqlLines = new List<string>();
        bool inSql = false;
        
        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("sql:"))
            {
                inSql = true;
                var content = line.Substring(line.IndexOf("sql:") + 4).Trim();
                if (content == "|" || content == ">")
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(content))
                {
                    sqlLines.Add(content);
                }
                continue;
            }
            if (inSql && !string.IsNullOrWhiteSpace(line))
            {
                sqlLines.Add(line.Trim());
            }
        }
        
        return string.Join(" ", sqlLines).Trim().TrimEnd(';');
    }

    private static string MockCallLLM(string prompt)
    {
        if (prompt.Contains("total products per category"))
        {
            return @"```yaml
sql: |
  SELECT category, COUNT(*) as total_products, SUM(price * stock_quantity) as total_value
  FROM products
  GROUP BY category
  ORDER BY total_value DESC
```";
        }
        return @"```yaml
sql: |
  SELECT * FROM products
```";
    }
}

class ExecuteSQLNode : Node<TShared, (string DbPath, string Sql), (bool Success, object? Result, List<string> Columns)>
{
    public override Task<(string DbPath, string Sql)?> Prep(TShared shared)
    {
        var dbPath = shared.TryGetValue("db_path", out var db) ? db as string ?? "ecommerce.db" : "ecommerce.db";
        var sql = shared.TryGetValue("generated_sql", out var s) ? s as string ?? "" : "";
        return Task.FromResult<(string, string)?>((dbPath, sql));
    }

    public override Task<(bool Success, object? Result, List<string> Columns)> Exec((string DbPath, string Sql) prepRes)
    {
        var (dbPath, sql) = prepRes;
        
        try
        {
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            
            var isSelect = sql.Trim().ToUpper().StartsWith("SELECT") || sql.Trim().ToUpper().StartsWith("WITH");
            
            if (isSelect)
            {
                var results = new List<object[]>();
                var columns = new List<string>();
                
                using var reader = cmd.ExecuteReader();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(reader.GetName(i));
                }
                
                while (reader.Read())
                {
                    var row = new object[reader.FieldCount];
                    reader.GetValues(row);
                    results.Add(row);
                }
                
                return Task.FromResult<(bool, object?, List<string>)>((true, results, columns));
            }
            else
            {
                var rowsAffected = cmd.ExecuteNonQuery();
                return Task.FromResult<(bool, object?, List<string>)>((true, $"Query OK. Rows affected: {rowsAffected}", new List<string>()));
            }
        }
        catch (SqliteException e)
        {
            Console.WriteLine($"SQLite Error during execution: {e.Message}");
            return Task.FromResult<(bool, object?, List<string>)>((false, e.Message, new List<string>()));
        }
    }

    public override Task<string?> Post(TShared shared, (string DbPath, string Sql)? prepRes, (bool Success, object? Result, List<string> Columns) execRes)
    {
        var (success, result, columns) = execRes;
        
        if (success)
        {
            shared["final_result"] = result;
            shared["result_columns"] = columns;
            
            Console.WriteLine("\n===== SQL EXECUTION SUCCESS =====\n");
            
            if (result is List<object[]> rows && rows.Count > 0)
            {
                if (columns.Count > 0)
                {
                    Console.WriteLine(string.Join(" | ", columns));
                    Console.WriteLine(new string('-', columns.Sum(c => c.Length) + 3 * (columns.Count - 1)));
                }
                foreach (var row in rows)
                {
                    Console.WriteLine(string.Join(" | ", row.Select(o => o?.ToString() ?? "NULL")));
                }
            }
            else if (result is string msg)
            {
                Console.WriteLine(msg);
            }
            Console.WriteLine("\n=================================\n");
            
            return Task.FromResult<string?>(null);
        }
        else
        {
            shared["execution_error"] = result?.ToString() ?? "Unknown error";
            shared["debug_attempts"] = (shared.TryGetValue("debug_attempts", out var d) ? (int)d : 0) + 1;
            var maxAttempts = shared.TryGetValue("max_debug_attempts", out var m) ? (int)m : 3;
            
            Console.WriteLine($"\n===== SQL EXECUTION FAILED (Attempt {shared["debug_attempts"]}) =====\n");
            Console.WriteLine($"Error: {shared["execution_error"]}");
            Console.WriteLine("=========================================\n");
            
            if ((int)shared["debug_attempts"] >= maxAttempts)
            {
                Console.WriteLine($"Max debug attempts ({maxAttempts}) reached. Stopping.");
                shared["final_error"] = $"Failed to execute SQL after {maxAttempts} attempts. Last error: {shared["execution_error"]}";
                return Task.FromResult<string?>(null);
            }
            
            Console.WriteLine("Attempting to debug the SQL...");
            return Task.FromResult<string?>("error_retry");
        }
    }
}

class DebugSQLNode : Node<TShared, (string? Query, string? Schema, string? FailedSql, string? Error), string>
{
    public override Task<(string? Query, string? Schema, string? FailedSql, string? Error)?> Prep(TShared shared)
    {
        var query = shared.TryGetValue("natural_query", out var q) ? q as string : null;
        var schema = shared.TryGetValue("schema", out var s) ? s as string : null;
        var failedSql = shared.TryGetValue("generated_sql", out var f) ? f as string : null;
        var error = shared.TryGetValue("execution_error", out var e) ? e as string : null;
        
        return Task.FromResult<(string?, string?, string?, string?)?>((query, schema, failedSql, error)));
    }

    public override Task<string> Exec((string? Query, string? Schema, string? FailedSql, string? Error) prepRes)
    {
        var (query, schema, failedSql, error) = prepRes;
        
        var prompt = $@"The following SQLite SQL query failed:
```sql
{failedSql}
```
It was generated for: ""{query}""
Schema:
{schema}
Error: ""{error}""

Provide a corrected SQLite query.

Respond ONLY with a YAML block containing the corrected SQL under the key 'sql':
```yaml
sql: |
  SELECT ... -- corrected query
```";

        Console.WriteLine("[DebugSQL] Generating corrected SQL...");
        var response = MockCallLLM(prompt);
        
        var yamlStr = response.Split("```yaml")[1].Split("```")[0].Trim();
        var correctedSql = ExtractSqlFromYaml(yamlStr);
        
        return Task.FromResult(correctedSql);
    }

    public override Task<string?> Post(TShared shared, (string?, string?, string?, string?)? prepRes, string execRes)
    {
        shared["generated_sql"] = execRes;
        shared.Remove("execution_error");
        
        Console.WriteLine($"\n===== REVISED SQL (Attempt {shared.TryGetValue("debug_attempts", out var d) ? (int)d : 0}) =====\n");
        Console.WriteLine(execRes);
        Console.WriteLine("\n====================================\n");
        
        return Task.FromResult<string?>("default");
    }

    private static string ExtractSqlFromYaml(string yaml)
    {
        var lines = yaml.Split('\n');
        var sqlLines = new List<string>();
        bool inSql = false;
        
        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("sql:"))
            {
                inSql = true;
                var content = line.Substring(line.IndexOf("sql:") + 4).Trim();
                if (content == "|" || content == ">")
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(content))
                {
                    sqlLines.Add(content);
                }
                continue;
            }
            if (inSql && !string.IsNullOrWhiteSpace(line))
            {
                sqlLines.Add(line.Trim());
            }
        }
        
        return string.Join(" ", sqlLines).Trim().TrimEnd(';');
    }

    private static string MockCallLLM(string prompt)
    {
        if (prompt.Contains("no such column", StringComparison.OrdinalIgnoreCase))
        {
            return @"```yaml
sql: |
  SELECT category, COUNT(*) as total_products FROM products GROUP BY category
```";
        }
        return @"```yaml
sql: |
  SELECT * FROM products
```";
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var query = args.Length > 0 ? string.Join(" ", args) : "total products per category";
        var dbPath = "ecommerce.db";
        
        if (!File.Exists(dbPath) || new FileInfo(dbPath).Length == 0)
        {
            Console.WriteLine($"Database at {dbPath} missing or empty. Populating...");
            PopulateDatabase(dbPath);
        }

        var shared = new Dictionary<string, object>
        {
            ["db_path"] = dbPath,
            ["natural_query"] = query,
            ["max_debug_attempts"] = 3,
            ["debug_attempts"] = 0,
            ["final_result"] = null,
            ["final_error"] = null
        };

        Console.WriteLine($"\n=== Starting Text-to-SQL Workflow ===");
        Console.WriteLine($"Query: '{query}'");
        Console.WriteLine($"Database: {dbPath}");
        Console.WriteLine($"Max Debug Retries on SQL Error: 3");
        Console.WriteLine("=" * 45);

        var getSchema = new GetSchemaNode();
        var generateSql = new GenerateSQLNode();
        var executeSql = new ExecuteSQLNode();
        var debugSql = new DebugSQLNode();

        getSchema >> generateSql >> executeSql;
        executeSql.On("error_retry").To(debugSql);
        debugSql.On("default").To(executeSql);

        var flow = new Flow<TShared>(getSchema);
        await flow.RunAsync(shared);

        if (shared.TryGetValue("final_error", out var error) && error != null)
        {
            Console.WriteLine("\n=== Workflow Completed with Error ===");
            Console.WriteLine($"Error: {error}");
        }
        else if (shared.TryGetValue("final_result", out var result) && result != null)
        {
            Console.WriteLine("\n=== Workflow Completed Successfully ===");
        }
        else
        {
            Console.WriteLine("\n=== Workflow Completed (Unknown State) ===");
        }

        Console.WriteLine("=" * 36);
    }

    static void PopulateDatabase(string dbPath)
    {
        if (File.Exists(dbPath))
            File.Delete(dbPath);

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        var createCustomersTable = @"CREATE TABLE customers (
            customer_id INTEGER PRIMARY KEY AUTOINCREMENT,
            first_name TEXT NOT NULL,
            last_name TEXT NOT NULL,
            email TEXT UNIQUE NOT NULL,
            registration_date DATE NOT NULL,
            city TEXT,
            country TEXT DEFAULT 'USA'
        );";
        
        var createProductsTable = @"CREATE TABLE products (
            product_id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            description TEXT,
            category TEXT NOT NULL,
            price REAL NOT NULL CHECK (price > 0),
            stock_quantity INTEGER NOT NULL DEFAULT 0 CHECK (stock_quantity >= 0)
        );";
        
        var createOrdersTable = @"CREATE TABLE orders (
            order_id INTEGER PRIMARY KEY AUTOINCREMENT,
            customer_id INTEGER NOT NULL,
            order_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            status TEXT NOT NULL CHECK (status IN ('pending', 'processing', 'shipped', 'delivered', 'cancelled')),
            total_amount REAL,
            shipping_address TEXT,
            FOREIGN KEY (customer_id) REFERENCES customers (customer_id)
        );";
        
        var createOrderItemsTable = @"CREATE TABLE order_items (
            order_item_id INTEGER PRIMARY KEY AUTOINCREMENT,
            order_id INTEGER NOT NULL,
            product_id INTEGER NOT NULL,
            quantity INTEGER NOT NULL CHECK (quantity > 0),
            price_per_unit REAL NOT NULL,
            FOREIGN KEY (order_id) REFERENCES orders (order_id),
            FOREIGN KEY (product_id) REFERENCES products (product_id)
        );";

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = createCustomersTable;
            cmd.ExecuteNonQuery();
            Console.WriteLine("Created 'customers' table.");
            
            cmd.CommandText = createProductsTable;
            cmd.ExecuteNonQuery();
            Console.WriteLine("Created 'products' table.");
            
            cmd.CommandText = createOrdersTable;
            cmd.ExecuteNonQuery();
            Console.WriteLine("Created 'orders' table.");
            
            cmd.CommandText = createOrderItemsTable;
            cmd.ExecuteNonQuery();
            Console.WriteLine("Created 'order_items' table.");
        }

        var customers = new[]
        {
            ("Alice", "Smith", "alice.s@email.com", "2023-01-15", "New York", "USA"),
            ("Bob", "Johnson", "b.johnson@email.com", "2023-02-20", "Los Angeles", "USA"),
            ("Charlie", "Williams", "charlie.w@email.com", "2023-03-10", "Chicago", "USA"),
            ("Diana", "Brown", "diana.b@email.com", "2023-04-05", "Houston", "USA"),
            ("Ethan", "Davis", "ethan.d@email.com", "2023-05-12", "Phoenix", "USA")
        };

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO customers (first_name, last_name, email, registration_date, city, country) VALUES (@fn, @ln, @email, @date, @city, @country)";
            foreach (var c in customers)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@fn", c.Item1);
                cmd.Parameters.AddWithValue("@ln", c.Item2);
                cmd.Parameters.AddWithValue("@email", c.Item3);
                cmd.Parameters.AddWithValue("@date", c.Item4);
                cmd.Parameters.AddWithValue("@city", c.Item5);
                cmd.Parameters.AddWithValue("@country", c.Item6);
                cmd.ExecuteNonQuery();
            }
            Console.WriteLine($"Inserted {customers.Length} customers.");
        }

        var products = new[]
        {
            ("Laptop Pro", "High-end laptop", "Electronics", 1200.00, 50),
            ("Wireless Mouse", "Ergonomic mouse", "Accessories", 25.50, 200),
            ("Mechanical Keyboard", "RGB keyboard", "Accessories", 75.00, 150),
            ("4K Monitor", "27-inch monitor", "Electronics", 350.00, 80),
            ("Smartphone X", "Latest smartphone", "Electronics", 999.00, 120),
            ("Coffee Maker", "Drip coffee maker", "Home Goods", 50.00, 300),
            ("Running Shoes", "Comfortable shoes", "Apparel", 90.00, 250),
            ("Yoga Mat", "Eco-friendly mat", "Sports", 30.00, 400),
            ("Desk Lamp", "LED desk lamp", "Home Goods", 45.00, 180),
            ("Backpack", "Durable backpack", "Accessories", 60.00, 220)
        };

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO products (name, description, category, price, stock_quantity) VALUES (@name, @desc, @cat, @price, @stock)";
            foreach (var p in products)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@name", p.Item1);
                cmd.Parameters.AddWithValue("@desc", p.Item2);
                cmd.Parameters.AddWithValue("@cat", p.Item3);
                cmd.Parameters.AddWithValue("@price", p.Item4);
                cmd.Parameters.AddWithValue("@stock", p.Item5);
                cmd.ExecuteNonQuery();
            }
            Console.WriteLine($"Inserted {products.Length} products.");
        }

        var statuses = new[] { "pending", "processing", "shipped", "delivered", "cancelled" };
        var random = new Random(42);
        
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO orders (customer_id, order_date, status, total_amount, shipping_address) VALUES (@cid, @date, @status, @total, @addr)";
            for (int i = 1; i <= 20; i++)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@cid", random.Next(1, 6));
                cmd.Parameters.AddWithValue("@date", DateTime.Now.AddDays(-random.Next(1, 60)));
                cmd.Parameters.AddWithValue("@status", statuses[random.Next(statuses.Length)]);
                cmd.Parameters.AddWithValue("@total", random.Next(50, 500) * 1.0);
                cmd.Parameters.AddWithValue("@addr", $"{random.Next(100, 999)} Main St, Anytown");
                cmd.ExecuteNonQuery();
            }
            Console.WriteLine("Inserted 20 orders.");
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO order_items (order_id, product_id, quantity, price_per_unit) VALUES (@oid, @pid, @qty, @price)";
            for (int orderId = 1; orderId <= 20; orderId++)
            {
                var numItems = random.Next(1, 5);
                for (int j = 0; j < numItems; j++)
                {
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@oid", orderId);
                    cmd.Parameters.AddWithValue("@pid", random.Next(1, 11));
                    cmd.Parameters.AddWithValue("@qty", random.Next(1, 6));
                    cmd.Parameters.AddWithValue("@price", products[random.Next(products.Length)].Item4);
                    cmd.ExecuteNonQuery();
                }
            }
            Console.WriteLine("Inserted order items.");
        }

        Console.WriteLine($"Database '{dbPath}' created and populated successfully.");
    }
}
