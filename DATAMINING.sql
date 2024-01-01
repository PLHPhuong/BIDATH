SELECT FACT.InvoiceID_sk, FACT.InvoiceID_nk, FACT.NumberOfCustomer, PRO.ProductID_NK, PRO.ProductLine, FACT.UnitPrice, FACT.Quantity, FACT.Tax_5_percent, FACT.Total, FACT.gross_income, FACT.gross_margin_percentage, FACT.cogs,FACT.Rating, C.Branch,C.City,CUS.Gender,CUS.CustomerType,PAY.Payment, D.Date, D.Year,D.Month
FROM	[BI_DDS].[dbo].[FactSupermarketSale] FACT
	LEFT JOIN [BI_DDS].[dbo].[DimProduct] PRO ON FACT.ProductID_SK=PRO.ProductID_SK
	LEFT JOIN [BI_DDS].[dbo].[DimCity] C ON FACT.CityID_SK=C.CityID_SK
	LEFT JOIN [BI_DDS].[dbo].[DimCustomer] CUS ON FACT.CustomerID_SK=CUS.CustomerID_SK
	LEFT JOIN [BI_DDS].[dbo].[DimPayment] PAY ON FACT.PaymentID_SK=PAY.PaymentID_SK
	LEFT JOIN [BI_DDS].[dbo].[DimDate] D ON FACT.Date_SK=D.Date_SK

-- Enable xp_cmdshell if not already enabled
-- EXEC sp_configure 'show advanced options', 1;
-- RECONFIGURE;
-- EXEC sp_configure 'xp_cmdshell', 1;
-- RECONFIGURE;

-- Replace 'YourDatabase' with your actual database name
-- Replace 'C:\Path\To\Output\File.csv' with the desired file path and name

DECLARE @cmd NVARCHAR(1000);

SET @cmd = 'bcp "SELECT FACT.InvoiceID_sk, FACT.InvoiceID_nk, FACT.NumberOfCustomer, PRO.ProductID_NK, PRO.ProductLine, FACT.UnitPrice, FACT.Quantity, FACT.Tax_5_percent, FACT.Total, FACT.gross_income, FACT.gross_margin_percentage, FACT.cogs,FACT.Rating, C.Branch,C.City,CUS.CustomerType,CUS.CustomerType,PAY.Payment, D.Date, D.Year,D.Month FROM [BI_DDS].[dbo].[FactSupermarketSale] FACT LEFT JOIN [BI_DDS].[dbo].[DimProduct] PRO ON FACT.ProductID_SK=PRO.ProductID_SK LEFT JOIN [BI_DDS].[dbo].[DimCity] C ON FACT.CityID_SK=C.CityID_SK LEFT JOIN [BI_DDS].[dbo].[DimCustomer] CUS ON FACT.CustomerID_SK=CUS.CustomerID_SK LEFT JOIN [BI_DDS].[dbo].[DimPayment] PAY ON FACT.PaymentID_SK=PAY.PaymentID_SK LEFT JOIN [BI_DDS].[dbo].[DimDate] D ON FACT.Date_SK=D.Date_SK" queryout "D:\BI\datasource.csv" -c -t, -T -S YourServerName';

EXEC xp_cmdshell @cmd;

GO

INSERT INTO OPENROWSET('Microsoft.ACE.OLEDB.16.0', 
    'Text;Database=C:\Path\To\Output\;HDR=YES;', 
    'SELECT * FROM YourQuery')