using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Data.SqlClient;
using System.Data;
using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace FunctionalApplication
{
    public enum HandleSqlConnection
    {
        OpenAndClose,
        OpenAndKeep
    }
    public enum HandleExistence
    {
        DoNothing,
        Truncate,
        Delete
    }
    public enum DeleteMethod
    {
        Truncate,
        Delete
    }
    class UnityFunction
    {
        public static HandleExistence fFormatExistedHandler(string method)
        {
            method = method.ToLower();
            switch (method)
            {
                case "donothing":
                    return HandleExistence.DoNothing;
                case "truncate":
                    return HandleExistence.Truncate;
                case "delete":
                    return HandleExistence.Delete;
                default:
                    throw new ArgumentException("method doesn't exist");
            }
        }
        public static bool fDatabaseExisted(ref SqlConnection connection, string databaseName, HandleSqlConnection? HandleSqlConnenctionOption = null)
        {
            // Check the connection state and open it if closed.
            var currentConnectionState = connection.State;
            if (currentConnectionState == ConnectionState.Closed) { connection.Open(); }

            // Check db existence
            string queryCheckDBExistence = $"SELECT COUNT(*) FROM sys.databases WHERE name = '{databaseName}'";
            int databaseCount = 0;
            using (SqlCommand command = new SqlCommand(queryCheckDBExistence, connection)) { databaseCount = (int)command.ExecuteScalar(); }

            // change back to current connection state or new connect state based on ConnectionOption
            if ((!HandleSqlConnenctionOption.HasValue && currentConnectionState == ConnectionState.Closed) || (HandleSqlConnenctionOption == HandleSqlConnection.OpenAndClose)) { connection.Close(); }
            return databaseCount > 0;
        }

        public static bool fTableExistedInDatabase(ref SqlConnection connection, string databaseName, string tableName, bool? AdreadyCheckDBIsExistedResult = null, HandleSqlConnection? HandleSqlConnenctionOption = null)
        {
            // Check the connection state and open it if closed.
            var currentConnectionState = connection.State;
            if (currentConnectionState == ConnectionState.Closed) { connection.Open(); }

            // Check db existence
            bool dbIsExited = AdreadyCheckDBIsExistedResult ?? fDatabaseExisted(ref connection, databaseName);
            if (!dbIsExited) { throw new ArgumentException($"{databaseName} not existed"); }

            // Check table existed
            string checkTableIsExistedQuery = $"SELECT COUNT(*) FROM {databaseName}.INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}'";
            int tableIsExistedQuery = 0;
            using (SqlCommand command = new SqlCommand(checkTableIsExistedQuery, connection)) { tableIsExistedQuery = Convert.ToInt32(command.ExecuteScalar()); }

            // Change back to current connection state or new connect state based on ConnectionOption
            if ((!HandleSqlConnenctionOption.HasValue && currentConnectionState == ConnectionState.Closed) || (HandleSqlConnenctionOption == HandleSqlConnection.OpenAndClose)) { connection.Close(); }

            return tableIsExistedQuery > 0;
        }

        public static void fDeleteTableInDatabase(ref SqlConnection connection, string databaseName, string tableName, DeleteMethod method = DeleteMethod.Truncate, bool? AdreadyCheckDBIsExistedResult = null, bool? AdreadyCheckTableIsExistedResult = null, bool ChangeDatabaseCurrentlyUse = false, HandleSqlConnection? HandleSqlConnenctionOption = null)
        {
            // Check the connection state and open it if closed.
            var currentConnectionState = connection.State;
            if (currentConnectionState == ConnectionState.Closed) { connection.Open(); }

            // Check db existence and table existed (will be checked in fTableExistedInDatabase)
            bool tableIsExisted = AdreadyCheckTableIsExistedResult ?? fTableExistedInDatabase(ref connection, databaseName, tableName, AdreadyCheckDBIsExistedResult, HandleSqlConnection.OpenAndKeep);

            // Change db context:
            string DBCurrentlyUsing;
            string queryGetDBCurrentlyUsing = $"SELECT DB_NAME()";
            using (SqlCommand command = new SqlCommand(queryGetDBCurrentlyUsing, connection)) { DBCurrentlyUsing = command.ExecuteScalar().ToString(); }
            if (queryGetDBCurrentlyUsing != databaseName)
            {
                string queryChangeDatabaseConnection = $"USE {databaseName}";
                using (SqlCommand command = new SqlCommand(queryChangeDatabaseConnection, connection)) { command.ExecuteNonQuery(); }
            }

            // return if table not existed
            if (tableIsExisted) { Console.WriteLine($"[{databaseName}].[dbo].[{tableName}] not existed"); return; }

            // Find table and ref table
            List<string> EffectedTable = new List<string>();

            Queue<string> tbNames = new Queue<string>();
            tbNames.Enqueue(tableName);
            while (tbNames.Count > 0)
            {
                string currentTableName = tbNames.Dequeue();
                EffectedTable.Add(currentTableName);
                string query = @"
                        SELECT
                            fk.name AS ForeignKeyName,
                            tp.name AS TableName,
                            cp.name AS ColumnName
                        FROM
                            sys.foreign_keys AS fk
                        INNER JOIN
                            sys.tables AS tp ON fk.parent_object_id = tp.object_id
                        INNER JOIN
                            sys.tables AS tr ON fk.referenced_object_id = tr.object_id
                        INNER JOIN
                            sys.foreign_key_columns AS fkc ON fk.object_id = fkc.constraint_object_id
                        INNER JOIN
                            sys.columns AS cp ON fkc.parent_column_id = cp.column_id AND fkc.parent_object_id = cp.object_id
                        INNER JOIN
                            sys.columns AS cr ON fkc.referenced_column_id = cr.column_id AND fkc.referenced_object_id = cr.object_id
                        WHERE
                            tr.name = @TableName";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@TableName", tableName);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string referencingTableName = reader["TableName"].ToString();
                            if (!EffectedTable.Contains(referencingTableName) && !tbNames.Contains(referencingTableName))
                            {
                                tbNames.Enqueue(referencingTableName);
                            }
                        }
                    }
                }
            }

            //Console.WriteLine(string.Join(", ", EffectedTable));

            // Delete or truncate tables
            string queryDeleteOrTruncate = $"{(method == DeleteMethod.Truncate ? "TRUNCATE TABLE " : "DELETE FROM ")}";
            for (int iterater = EffectedTable.Count - 1; iterater >= 0; iterater--)
            {
                string query = queryDeleteOrTruncate + EffectedTable[iterater];
                using (SqlCommand command = new SqlCommand(query, connection)) { command.ExecuteScalar(); }
            }

            // Change back to original db
            if (!ChangeDatabaseCurrentlyUse) { using (SqlCommand command = new SqlCommand($"USE {DBCurrentlyUsing}", connection)) { command.ExecuteNonQuery(); } }

            // change back to current connection state or new connect state based on ConnectionOption
            if ((!HandleSqlConnenctionOption.HasValue && currentConnectionState == ConnectionState.Closed) || (HandleSqlConnenctionOption == HandleSqlConnection.OpenAndClose)) { connection.Close(); }
        }

        public static void fCreateTableInDatabase(ref SqlConnection connection, string databaseName, string tableName, string createTableQuery, HandleExistence method = HandleExistence.Truncate, bool? AdreadyCheckDBIsExistedResult = null, bool? AdreadyCheckTableIsExistedResult = null, bool ChangeDatabaseCurrentlyUse = false, HandleSqlConnection? HandleSqlConnenctionOption = null)
        {
            // Check the connection state and open it if closed.
            var currentConnectionState = connection.State;
            if (currentConnectionState == ConnectionState.Closed) { connection.Open(); }

            // Check db existence and table existed (will be checked in fTableExistedInDatabase)
            bool tableIsExisted = AdreadyCheckTableIsExistedResult ?? fTableExistedInDatabase(ref connection, databaseName, tableName, AdreadyCheckDBIsExistedResult, HandleSqlConnection.OpenAndKeep);

            // Change db context:
            string DBCurrentlyUsing;
            string queryGetDBCurrentlyUsing = $"SELECT DB_NAME()";
            using (SqlCommand command = new SqlCommand(queryGetDBCurrentlyUsing, connection)) { DBCurrentlyUsing = command.ExecuteScalar().ToString(); }
            if (queryGetDBCurrentlyUsing != databaseName)
            {
                string queryChangeDatabaseConnection = $"USE {databaseName}";
                using (SqlCommand command = new SqlCommand(queryChangeDatabaseConnection, connection)) { command.ExecuteNonQuery(); }
            }

            // Delete or Trucate existed table
            if (tableIsExisted)
            {
                switch (method)
                {
                    case HandleExistence.DoNothing:
                        Console.WriteLine($"[{databaseName}].[{tableName}] existed and how to handle existed is DoNothing so there aren't action to take");
                        return;
                    case HandleExistence.Truncate:
                        fDeleteTableInDatabase(ref connection, databaseName, tableName, DeleteMethod.Truncate, AdreadyCheckDBIsExistedResult, tableIsExisted, false, HandleSqlConnection.OpenAndKeep);
                        Console.WriteLine($"[{databaseName}].[{tableName}] has been truncated");
                        return;
                    case HandleExistence.Delete:
                        fDeleteTableInDatabase(ref connection, databaseName, tableName, DeleteMethod.Delete, AdreadyCheckDBIsExistedResult, tableIsExisted, false, HandleSqlConnection.OpenAndKeep);
                        break;
                }
            }
            // Create table
            using (SqlCommand command = new SqlCommand(createTableQuery, connection)) { command.ExecuteScalar(); }


            // Change back to original db
            if (!ChangeDatabaseCurrentlyUse) { using (SqlCommand command = new SqlCommand($"USE {DBCurrentlyUsing}", connection)) { command.ExecuteNonQuery(); } }

            // change back to current connection state or new connect state based on ConnectionOption
            if ((!HandleSqlConnenctionOption.HasValue && currentConnectionState == ConnectionState.Closed) || (HandleSqlConnenctionOption == HandleSqlConnection.OpenAndClose)) { connection.Close(); }

        }

        public static bool fTriggerExistedInDatabase(ref SqlConnection connection, string databaseName, string triggerName, bool? AdreadyCheckDBIsExistedResult = null, HandleSqlConnection? HandleSqlConnenctionOption = null)
        {
            // Check the connection state and open it if closed.
            var currentConnectionState = connection.State;
            if (currentConnectionState == ConnectionState.Closed) { connection.Open(); }

            // Check db existence
            bool dbIsExited = AdreadyCheckDBIsExistedResult ?? fDatabaseExisted(ref connection, databaseName);
            if (!dbIsExited) { throw new ArgumentException($"{databaseName} not existed"); }

            // Check table existed
            string checkTriggerQuery = $"SELECT COUNT(*) FROM {databaseName}.sys.triggers WHERE name = '{triggerName}'";
            int triggerIsExisted = 0;
            using (SqlCommand command = new SqlCommand(checkTriggerQuery, connection)) { triggerIsExisted = Convert.ToInt32(command.ExecuteScalar()); }

            // Change back to current connection state or new connect state based on ConnectionOption
            if ((!HandleSqlConnenctionOption.HasValue && currentConnectionState == ConnectionState.Closed) || (HandleSqlConnenctionOption == HandleSqlConnection.OpenAndClose)) { connection.Close(); }

            return triggerIsExisted > 0;
        }

        public static void fDeleteTriggerInDatabase(ref SqlConnection connection, string databaseName, string triggerName, bool? AdreadyCheckDBIsExistedResult = null, bool? AdreadyCheckTriggerIsExistedResult = null, bool ChangeDatabaseCurrentlyUse = false, HandleSqlConnection? HandleSqlConnenctionOption = null)
        {
            // Check the connection state and open it if closed.
            var currentConnectionState = connection.State;
            if (currentConnectionState == ConnectionState.Closed) { connection.Open(); }

            // Check db existence and trigger existed (will be checked in fTriggerExistedInDatabase)
            bool triggerIsExisted = AdreadyCheckTriggerIsExistedResult ?? fTriggerExistedInDatabase(ref connection, databaseName, triggerName, AdreadyCheckDBIsExistedResult, HandleSqlConnection.OpenAndKeep);

            // Change db context:
            string DBCurrentlyUsing;
            string queryGetDBCurrentlyUsing = $"SELECT DB_NAME()";
            using (SqlCommand command = new SqlCommand(queryGetDBCurrentlyUsing, connection)) { DBCurrentlyUsing = command.ExecuteScalar().ToString(); }
            if (queryGetDBCurrentlyUsing != databaseName)
            {
                string queryChangeDatabaseConnection = $"USE {databaseName}";
                using (SqlCommand command = new SqlCommand(queryChangeDatabaseConnection, connection)) { command.ExecuteNonQuery(); }
            }

            // return if table not existed
            if (triggerIsExisted) { Console.WriteLine($"[{databaseName}].[dbo].[{triggerName}] not existed"); return; }

            // Delete trigger
            string deleteTriggerQuery = $"DROP TRIGGER {triggerName}"; ;
            using (SqlCommand command = new SqlCommand(deleteTriggerQuery, connection)) { command.ExecuteScalar(); }

            // Change back to original db
            if (!ChangeDatabaseCurrentlyUse) { using (SqlCommand command = new SqlCommand($"USE {DBCurrentlyUsing}", connection)) { command.ExecuteNonQuery(); } }

            // change back to current connection state or new connect state based on ConnectionOption
            if ((!HandleSqlConnenctionOption.HasValue && currentConnectionState == ConnectionState.Closed) || (HandleSqlConnenctionOption == HandleSqlConnection.OpenAndClose)) { connection.Close(); }
        }

        public static void fCreateTriggerInDatabase(ref SqlConnection connection, string databaseName, string triggerName, string createTriggerQuery, HandleExistence method = HandleExistence.Delete, bool? AdreadyCheckDBIsExistedResult = null, bool? AdreadyCheckTriggerIsExistedResult = null, bool ChangeDatabaseCurrentlyUse = false, HandleSqlConnection? HandleSqlConnenctionOption = null)
        {
            // Check the connection state and open it if closed.
            var currentConnectionState = connection.State;
            if (currentConnectionState == ConnectionState.Closed) { connection.Open(); }

            // Check db existence and trigger existed (will be checked in fTriggerExistedInDatabase)
            bool triggerIsExisted = AdreadyCheckTriggerIsExistedResult ?? fTriggerExistedInDatabase(ref connection, databaseName, triggerName, AdreadyCheckDBIsExistedResult, HandleSqlConnection.OpenAndKeep);

            // Change db context:
            string DBCurrentlyUsing;
            string queryGetDBCurrentlyUsing = $"SELECT DB_NAME()";
            using (SqlCommand command = new SqlCommand(queryGetDBCurrentlyUsing, connection)) { DBCurrentlyUsing = command.ExecuteScalar().ToString(); }
            if (queryGetDBCurrentlyUsing != databaseName)
            {
                string queryChangeDatabaseConnection = $"USE {databaseName}";
                using (SqlCommand command = new SqlCommand(queryChangeDatabaseConnection, connection)) { command.ExecuteNonQuery(); }
            }

            // Delete or Trucate existed table
            if (triggerIsExisted)
            {
                switch (method)
                {
                    case HandleExistence.DoNothing:
                        Console.WriteLine($"[{databaseName}].[{triggerName}] existed and how to handle existed is DoNothing so there aren't action to take");
                        return;
                    case HandleExistence.Truncate:
                        throw new ArgumentException("Can not truncate trigger");
                    case HandleExistence.Delete:
                        string deleteTriggerQuery = $"DROP TRIGGER {triggerName}"; ;
                        using (SqlCommand command = new SqlCommand(deleteTriggerQuery, connection)) { command.ExecuteScalar(); }
                        break;
                }
            }
            // Create trigger
            using (SqlCommand command = new SqlCommand(createTriggerQuery, connection)) { command.ExecuteScalar(); }

            // Change back to original db
            if (!ChangeDatabaseCurrentlyUse) { using (SqlCommand command = new SqlCommand($"USE {DBCurrentlyUsing}", connection)) { command.ExecuteNonQuery(); } }

            // change back to current connection state or new connect state based on ConnectionOption
            if ((!HandleSqlConnenctionOption.HasValue && currentConnectionState == ConnectionState.Closed) || (HandleSqlConnenctionOption == HandleSqlConnection.OpenAndClose)) { connection.Close(); }

        }

        public static string fMyFormattedErrorMessage(Exception exception)
        {
            
            return $"Exception Name: {exception.GetType().Name} - Message: {exception.Message}\n" + fMyFormattedErrorMessage(exception.InnerException);
        }

        public static string fFindTargetFolder(string startDirectory, string targetFolderName)
        {
            DirectoryInfo currentDirectory = new DirectoryInfo(startDirectory);

            while (currentDirectory != null)
            {
                DirectoryInfo[] subdirectories = currentDirectory.GetDirectories(targetFolderName, SearchOption.TopDirectoryOnly);
                if (subdirectories.Length > 0)
                {
                    // Target folder found in the current directory or its parent
                    return currentDirectory.FullName;
                }
                currentDirectory = currentDirectory.Parent;
            }
            // Target folder not found in the current directory or any of its parents
            return null;
        }
        public static string fFindTargetFile(string startDirectory, string targetFileName)
        {
            DirectoryInfo currentDirectory = new DirectoryInfo(startDirectory);

            while (currentDirectory != null)
            {
                FileInfo[] files = currentDirectory.GetFiles(targetFileName, SearchOption.TopDirectoryOnly);

                if (files.Length > 0)
                {
                    // Target file found in the current directory or its parent
                    return files[0].FullName;
                }

                currentDirectory = currentDirectory.Parent;
            }

            // Target file not found in the current directory or any of its parents
            return null;
        }

        public static void fResolvePaths(XmlNode node,string basePath)
        {
            // Check if the parent node has child nodes
            if (node.HasChildNodes)
            {
                // Iterate through child nodes
                foreach (XmlNode childNode in node.ChildNodes)
                {
                    // Check if the child node is an element
                    
                    if (childNode.NodeType == XmlNodeType.Element)
                    {
                        // Recursively process child nodes for elements
                        fResolvePaths(childNode, basePath);

                        // Update the element (example: add an attribute)
                        XmlElement element = (XmlElement)childNode;
                        
                    }
                    else if (childNode.NodeType == XmlNodeType.Text)
                    {
                        string relativePath = childNode.Value.Trim();
                        if (!string.IsNullOrEmpty(relativePath))
                        {
                            relativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar);
                            childNode.Value = $"{basePath}{@"\"}{relativePath}".Replace('\\', Path.DirectorySeparatorChar);
                            childNode.Value = Path.GetFullPath(childNode.Value);
                        }
                        
                    }
                    
                }
            }
            else
            {
                node.InnerText = Path.GetFullPath(basePath);
            }
        }
    }
    class ApplicationFunction
    {
        public static void CreateNecessaryDatabase()
        {
            XmlDocument xmlDoc = new XmlDocument();

            // > Specified variables:
            // > Script:

            // >> Load Condfig
            XmlDocument tempXmlDoc = new XmlDocument();
            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string solutionDirectory = UnityFunction.fFindTargetFile(currentDirectory, "FilePathConfig.xml");
            if (solutionDirectory == null) throw new Exception("Cannot file config");
            tempXmlDoc.Load(solutionDirectory);
            string configFilePath = tempXmlDoc.SelectSingleNode("/Root/DatabaseCongfigFile").InnerText;



            xmlDoc.Load(configFilePath);
            string connectionString = xmlDoc.SelectSingleNode("/Root/SQLServerConnnectionString").InnerText;
            XmlNodeList databaseNodes = xmlDoc.SelectNodes("/Root/Databases/Database");


            HandleExistence DatabaseExistenceHandleMethod = UnityFunction.fFormatExistedHandler(xmlDoc.SelectSingleNode("/Root/HandleExistedMethod/Database").InnerText);
            HandleExistence DatabaseTableExistenceHandleMethod = UnityFunction.fFormatExistedHandler(xmlDoc.SelectSingleNode("/Root/HandleExistedMethod/Table").InnerText);
            HandleExistence DatabaseTriggerExistenceHandleMethod = UnityFunction.fFormatExistedHandler(xmlDoc.SelectSingleNode("/Root/HandleExistedMethod/Trigger").InnerText);

            // >> Handle:
            SqlConnection connection = new SqlConnection(connectionString);

            //UnityFunction.fDeleteTableFunc(ref connection, "AdventureWorks2012", "product", " ", HandleExistence.Delete);

            foreach (XmlNode databaseNode in databaseNodes)
            {
                string databaseName = databaseNode.SelectSingleNode("Name").InnerText;

                connection.Open();
                // ---- CREATE DATABASE ----:
                // CREATE DATABASE - step 1: check db existence
                bool databaseExisted = UnityFunction.fDatabaseExisted(ref connection, databaseName);

                // CREATE DATABASE - step 2: handle existence and create db
                if (databaseExisted && DatabaseExistenceHandleMethod == HandleExistence.Delete)
                {
                    // CREATE DATABASE - step 2.1: drop db
                    string queryDBExistenceHandle = $"DROP DATABASE {databaseName}";
                    using (SqlCommand command = new SqlCommand(queryDBExistenceHandle, connection)) { command.ExecuteScalar(); }
                    databaseExisted = false;
                }
                // CREATE DATABASE - step 2.2: create db
                if (!databaseExisted)
                {
                    string queryDBCreation = $"CREATE DATABASE {databaseName}";
                    using (SqlCommand command = new SqlCommand(queryDBCreation, connection)) { command.ExecuteScalar(); }
                    databaseExisted = true;
                }

                // ---- CREATE METATABLE FOR DB ----:
                XmlNodeList MetaTables = databaseNode.SelectNodes("MetaTables/Table");
                if (MetaTables != null)
                {
                    foreach (XmlNode table in MetaTables)
                    {
                        string tableName = table.SelectSingleNode("Name").InnerText;
                        string createTableQuery = table.SelectSingleNode("CreationQuery").InnerText;
                        UnityFunction.fCreateTableInDatabase(ref connection, databaseName, tableName, createTableQuery, DatabaseTableExistenceHandleMethod, true, false, true, HandleSqlConnection.OpenAndKeep);
                    }
                }
                // ---- CREATE TRIGGER FOR DB ----:
                XmlNodeList Triggers = databaseNode.SelectNodes("TriggerOnDatabase/Trigger");
                if (Triggers != null)
                {
                    foreach (XmlNode trigger in Triggers)
                    {
                        string tableName = trigger.SelectSingleNode("Name").InnerText;
                        string createTriggerQuery = trigger.SelectSingleNode("CreationQuery").InnerText;
                        UnityFunction.fCreateTriggerInDatabase(ref connection, databaseName, tableName, createTriggerQuery, DatabaseTriggerExistenceHandleMethod, true, false, true, HandleSqlConnection.OpenAndKeep);
                    }
                }
                // ---- CREATE TABLE FOR DB ----:
                XmlNodeList Tables = databaseNode.SelectNodes("Tables/Table");
                if (Tables != null)
                {
                    foreach (XmlNode table in Tables)
                    {
                        string tableName = table.SelectSingleNode("Name").InnerText;
                        string createTableQuery = table.SelectSingleNode("CreationQuery").InnerText;
                        UnityFunction.fCreateTableInDatabase(ref connection, databaseName, tableName, createTableQuery, DatabaseTableExistenceHandleMethod, true, false, true, HandleSqlConnection.OpenAndKeep);
                    }
                    connection.Close();
                }
            }
        }
        public static void CreateNecessaryDatabase(List<string> typesNames = null, List<string> databasesNames = null)
        {
            if (typesNames == null && databasesNames == null) { CreateNecessaryDatabase(); return; }
            XmlDocument xmlDoc = new XmlDocument();

            // > Specified variables:
            // > Script:

            // >> Load Condfig
            XmlDocument tempXmlDoc = new XmlDocument();
            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string solutionDirectory = UnityFunction.fFindTargetFile(currentDirectory, "FilePathConfig.xml");
            if (solutionDirectory == null) throw new Exception("Cannot file config");
            tempXmlDoc.Load(solutionDirectory);
            string configFilePath = tempXmlDoc.SelectSingleNode("/Root/DatabaseCongfigFile").InnerText;



            xmlDoc.Load(configFilePath);
            string connectionString = xmlDoc.SelectSingleNode("/Root/SQLServerConnnectionString").InnerText;
            XmlNodeList databaseNodes = xmlDoc.SelectNodes("/Root/Databases/Database");


            HandleExistence DatabaseExistenceHandleMethod = UnityFunction.fFormatExistedHandler(xmlDoc.SelectSingleNode("/Root/HandleExistedMethod/Database").InnerText);
            HandleExistence DatabaseTableExistenceHandleMethod = UnityFunction.fFormatExistedHandler(xmlDoc.SelectSingleNode("/Root/HandleExistedMethod/Table").InnerText);
            HandleExistence DatabaseTriggerExistenceHandleMethod = UnityFunction.fFormatExistedHandler(xmlDoc.SelectSingleNode("/Root/HandleExistedMethod/Trigger").InnerText);

            // >> Handle:
            SqlConnection connection = new SqlConnection(connectionString);

            //UnityFunction.fDeleteTableFunc(ref connection, "AdventureWorks2012", "product", " ", HandleExistence.Delete);

            foreach (XmlNode databaseNode in databaseNodes)
            {
                if (!databasesNames.Contains(databaseNode.Attributes["Name"].Value) && !typesNames.Contains(databaseNode.Attributes["Type"].Value)) continue;
                string databaseName = databaseNode.SelectSingleNode("Name").InnerText;
                
                connection.Open();
                // ---- CREATE DATABASE ----:
                // CREATE DATABASE - step 1: check db existence
                bool databaseExisted = UnityFunction.fDatabaseExisted(ref connection, databaseName);

                // CREATE DATABASE - step 2: handle existence and create db
                if (databaseExisted && DatabaseExistenceHandleMethod == HandleExistence.Delete)
                {
                    // CREATE DATABASE - step 2.1: drop db
                    string queryDBExistenceHandle = $"DROP DATABASE {databaseName}";
                    using (SqlCommand command = new SqlCommand(queryDBExistenceHandle, connection)) { command.ExecuteScalar(); }
                    databaseExisted = false;
                }
                // CREATE DATABASE - step 2.2: create db
                if (!databaseExisted)
                {
                    string queryDBCreation = $"CREATE DATABASE {databaseName}";
                    using (SqlCommand command = new SqlCommand(queryDBCreation, connection)) { command.ExecuteScalar(); }
                    databaseExisted = true;
                }

                // ---- CREATE METATABLE FOR DB ----:
                XmlNodeList MetaTables = databaseNode.SelectNodes("MetaTables/Table");
                if (MetaTables != null)
                {
                    foreach (XmlNode table in MetaTables)
                    {
                        string tableName = table.SelectSingleNode("Name").InnerText;
                        string createTableQuery = table.SelectSingleNode("CreationQuery").InnerText;
                        UnityFunction.fCreateTableInDatabase(ref connection, databaseName, tableName, createTableQuery, DatabaseTableExistenceHandleMethod, true, false, true, HandleSqlConnection.OpenAndKeep);
                    }
                }
                // ---- CREATE TRIGGER FOR DB ----:
                XmlNodeList Triggers = databaseNode.SelectNodes("TriggerOnDatabase/Trigger");
                if (Triggers != null)
                {
                    foreach (XmlNode trigger in Triggers)
                    {
                        string tableName = trigger.SelectSingleNode("Name").InnerText;
                        string createTriggerQuery = trigger.SelectSingleNode("CreationQuery").InnerText;
                        UnityFunction.fCreateTriggerInDatabase(ref connection, databaseName, tableName, createTriggerQuery, DatabaseTriggerExistenceHandleMethod, true, false, true, HandleSqlConnection.OpenAndKeep);
                    }
                }
                // ---- CREATE TABLE FOR DB ----:
                XmlNodeList Tables = databaseNode.SelectNodes("Tables/Table");
                if (Tables != null)
                {
                    foreach (XmlNode table in Tables)
                    {
                        string tableName = table.SelectSingleNode("Name").InnerText;
                        string createTableQuery = table.SelectSingleNode("CreationQuery").InnerText;
                        UnityFunction.fCreateTableInDatabase(ref connection, databaseName, tableName, createTableQuery, DatabaseTableExistenceHandleMethod, true, false, true, HandleSqlConnection.OpenAndKeep);
                    }
                    connection.Close();
                }
            }

        }
        public static void SettingUpDirectory()
        {
            try
            {
                string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string solutionDirectory = UnityFunction.fFindTargetFolder(currentDirectory, ".git") ?? UnityFunction.fFindTargetFile(currentDirectory, "FilePathConfigTemplate.xml");
                if (solutionDirectory != null)
                {
                    // Note: D:\BI\DATH
                    string templateFilePath = $"{solutionDirectory}{@"\FilePathConfigTemplate.xml"}";
                    string filePath = $"{solutionDirectory}{@"\FilePathConfig.xml"}";

                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(templateFilePath);


                    // Get the root element
                    XmlElement root = xmlDoc.DocumentElement;
                    
                    // Resolve relative paths and update the XML document
                    UnityFunction.fResolvePaths(root, solutionDirectory);

                    // Save the updated XML document to the output file
                    xmlDoc.Save(filePath);


                    // Change some path in config.xml
                    XmlDocument xmlConfigDB = new XmlDocument();
                    string configXmlPath = xmlDoc.SelectSingleNode("/Root/DatabaseCongfigFile").InnerText;
                    xmlConfigDB.Load(configXmlPath);

                    XmlNodeList targetTriggers = xmlConfigDB.SelectNodes($"//Trigger[@Name='tr_CreateTable']");
                    // Iterate through the selected Trigger elements
                    foreach (XmlNode trigger in targetTriggers)
                    {
                        // Get the inner text of CreationQuery
                        XmlNode creationQueryNode = trigger.SelectSingleNode("CreationQuery");
                        string creationQueryInnerText = creationQueryNode.InnerText;

                        // Find and replace a substring in CreationQuery
                        string substringToReplace = "DECLARE @XmlFilePath NVARCHAR(255) = 'D:\\BI\\DATH\\BIDATH\\Config.xml';";
                        string replacementSubstring = $"DECLARE @XmlFilePath NVARCHAR(255) = '{configXmlPath}';";
                        string modifiedCreationQuery = creationQueryInnerText.Replace(substringToReplace, replacementSubstring);

                        // Update the inner text of CreationQuery
                        creationQueryNode.InnerText = modifiedCreationQuery;
                    }
                    xmlConfigDB.Save(configXmlPath);

                    Console.WriteLine($"File '{filePath}' created with absolute paths.");
                }
                else
                {
                    throw new Exception("Can not located project/solution directory");
                }

            } catch (Exception ex)
            {
                throw new Exception(UnityFunction.fMyFormattedErrorMessage(ex));
            }

        }
    }
    

    class Program
    {
        static void Main(string[] args)
        {
            List<(string,Action, bool)> ConsoleFunctions = new List<(string, Action, bool)>
            {
                ("SettingUpDirectory",ApplicationFunction.SettingUpDirectory,true),
                ("CreatenecessaryDatabase",ApplicationFunction.CreateNecessaryDatabase,true),
            };
           foreach (var (name,func,enable) in ConsoleFunctions)
            {
                if (enable) func.Invoke();
                Console.WriteLine($"Compete {name}");
            }
            
            Console.ReadLine();
        }

    }
}
