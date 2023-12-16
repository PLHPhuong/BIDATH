UPDATE AuditTableLog
SET CET = CreatedTime, LSET = CreatedTime;

SELECT * FROM AuditTableLog
+		User::CET_DimProduct	{12/16/2023 2:48:32 PM}	DateTime
+		User::LSET_DimProduct	{12/16/2023 2:48:32 PM}	DateTime

SELECT * FROM [BI_NDS].[dbo].[Product]
WHERE UpdatedDate > CONVERT(DATETIME2, '12/16/2023 2:48:32 PM', 101) AND UpdatedDate < CONVERT(DATETIME2, '12/16/2023 2:48:32 PM', 101) 