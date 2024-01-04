SELECT FACT.InvoiceID_sk, FACT.InvoiceID_nk, FACT.NumberOfCustomer, PRO.ProductID_NK, PRO.ProductLine, FACT.UnitPrice, FACT.Quantity, FACT.Tax_5_percent, FACT.Total, FACT.gross_income, FACT.gross_margin_percentage, FACT.cogs,FACT.Rating, C.Branch,C.City,CUS.Gender,CUS.CustomerType,PAY.Payment, D.Date, D.Year,D.Month
FROM	[BI_DDS].[dbo].[FactSupermarketSale] FACT
	LEFT JOIN [BI_DDS].[dbo].[DimProduct] PRO ON FACT.ProductID_SK=PRO.ProductID_SK
	LEFT JOIN [BI_DDS].[dbo].[DimCity] C ON FACT.CityID_SK=C.CityID_SK
	LEFT JOIN [BI_DDS].[dbo].[DimCustomer] CUS ON FACT.CustomerID_SK=CUS.CustomerID_SK
	LEFT JOIN [BI_DDS].[dbo].[DimPayment] PAY ON FACT.PaymentID_SK=PAY.PaymentID_SK
	LEFT JOIN [BI_DDS].[dbo].[DimDate] D ON FACT.Date_SK=D.Date_SK
