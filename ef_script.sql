CREATE TABLE [AIServiceLogs] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [PromptSnippet] nvarchar(max) NULL,
    [PromptTokens] int NOT NULL,
    [CompletionTokens] int NOT NULL,
    [Cost] decimal(18,4) NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    [ModelName] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_AIServiceLogs] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AspNetRoles] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AspNetUsers] (
    [Id] int NOT NULL IDENTITY,
    [AdSoyad] nvarchar(max) NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    [TokenBalance] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [CONNECTSON] (
    [TFSNO] float NULL,
    [MADDEBASLIK] nvarchar(255) NULL,
    [MADDEDURUM] nvarchar(255) NULL,
    [ACILMATARIHI] nvarchar(255) NULL,
    [ACANKULLANICI] nvarchar(255) NULL,
    [PROJE] nvarchar(255) NULL,
    [URUN] nvarchar(255) NULL,
    [MUSTERI_SORUMLUSU] nvarchar(255) NULL,
    [SATIS_SORUMLUSU] nvarchar(255) NULL,
    [YARATICI] nvarchar(255) NULL,
    [ConnectDurum] nvarchar(255) NULL
);
GO


CREATE TABLE [InventoryAccountProducts] (
    [LNGKOD] int NOT NULL IDENTITY,
    [Id] uniqueidentifier NULL,
    [StartDate] date NULL,
    [FinishDate] date NULL,
    [InvInstalledDate] date NULL,
    [InvPurchaseDate] date NULL,
    [Status] nvarchar(50) NULL,
    [StatusCrm] nvarchar(50) NULL,
    [FinancialProductType] nvarchar(50) NULL,
    [PFApplicaitonType] nvarchar(50) NULL,
    [AccountId] uniqueidentifier NULL,
    [StockId] uniqueidentifier NULL,
    [ContactId] uniqueidentifier NULL,
    [ContractId] uniqueidentifier NULL,
    [CompanyId] uniqueidentifier NULL,
    [ProductGroupId] uniqueidentifier NULL,
    [ProductSubGroupId] uniqueidentifier NULL,
    [QuoteId] uniqueidentifier NULL,
    [CrmOrderID] uniqueidentifier NULL,
    [Amount] int NULL,
    [Domain] nvarchar(150) NULL,
    [PosAccountNo] nvarchar(100) NULL,
    [CardNo] nvarchar(100) NULL,
    [InvSerialId] uniqueidentifier NULL,
    [InvOutOfWarehouseSerial] bit NULL,
    [InvOutOfWarehouseSerialCode] nvarchar(100) NULL,
    [IsSentToTsm] bit NULL,
    [PriceCurrency] nvarchar(10) NULL,
    [PriceAmount] decimal(18,4) NULL,
    [VatCurrency] nvarchar(10) NULL,
    [VatAmount] decimal(18,4) NULL,
    [TotalListPriceCurrency] nvarchar(10) NULL,
    [TotalListPriceAmount] decimal(18,4) NULL,
    [TotalPackagePriceCurrency] nvarchar(10) NULL,
    [TotalPackagePriceAmount] decimal(18,4) NULL,
    [TotalPackageVatCurrency] nvarchar(10) NULL,
    [TotalPackageVatAmount] decimal(18,4) NULL,
    [InstallCountryCode] nvarchar(10) NULL,
    [InstallState] nvarchar(100) NULL,
    [InstallProvince] nvarchar(100) NULL,
    [InstallDistrict] nvarchar(100) NULL,
    [InstallPlaceName] nvarchar(150) NULL,
    [InstallSubdivision1] nvarchar(100) NULL,
    [InstallSubdivision2] nvarchar(100) NULL,
    [InstallSubdivision3] nvarchar(100) NULL,
    [InstallSubdivision4] nvarchar(100) NULL,
    [InstallPostalCode] nvarchar(20) NULL,
    [InstallAddress] nvarchar(500) NULL,
    [InstallCity] nvarchar(100) NULL,
    [InstallLongitude] float NULL,
    [InstallLatitude] float NULL,
    [StockCode] nvarchar(50) NULL,
    [StockName] nvarchar(250) NULL,
    [TPOutReferenceCode] nvarchar(50) NULL,
    [SAPOutReferenceCode] nvarchar(50) NULL,
    [ApprovalCorrelationId] uniqueidentifier NULL,
    [ApprovalRequestedBy] nvarchar(150) NULL,
    [ApprovalRequestedOn] datetime2 NULL,
    [ApprovalAssignTo] nvarchar(150) NULL,
    [ApprovalRespondedOn] datetime2 NULL,
    [ApprovalState] nvarchar(50) NULL,
    [ApprovalResponse] nvarchar(250) NULL,
    [CreatedOn] datetime2 NULL,
    [CreatedBy] nvarchar(150) NULL,
    [CreatedPlatform] nvarchar(50) NULL,
    [ModifiedOn] datetime2 NULL,
    [ModifiedBy] nvarchar(150) NULL,
    [ModifiedPlatform] nvarchar(50) NULL,
    [DeletedOn] datetime2 NULL,
    [DeletedBy] nvarchar(150) NULL,
    [Tags] nvarchar(250) NULL,
    CONSTRAINT [PK__Inventor__E133217FCB727F2B] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [PARAMETRELER] (
    [Id] int NOT NULL IDENTITY,
    [ParametreAdi] nvarchar(max) NULL,
    [Deger] nvarchar(max) NULL,
    [Grup] nvarchar(max) NULL,
    [Durum] int NOT NULL,
    CONSTRAINT [PK_dbo.Parametrelers] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [STOKKODUESLESTIRME] (
    [URUN_ADI] nvarchar(255) NULL,
    [MODUL_ADI] nvarchar(255) NULL,
    [STOK_KODU] nvarchar(255) NULL,
    [STOK_ADI] nvarchar(255) NULL
);
GO


CREATE TABLE [TALEP_SON] (
    [TFSNO] int NOT NULL,
    [MADDEBASLIK] char(250) NULL,
    [MADDEDURUM] nvarchar(256) COLLATE Latin1_General_CI_AS NULL,
    [ACILMATARIHI] datetime NULL,
    [DEGISTIRMETARIHI] datetime NULL,
    [ACANKULLANICI] nvarchar(256) COLLATE Latin1_General_CI_AS NULL,
    [PROJE] nvarchar(50) COLLATE Latin1_General_CI_AS NULL,
    [COST] nvarchar(50) COLLATE Latin1_General_CI_AS NULL,
    [SATISDURUMU] nvarchar(50) COLLATE Latin1_General_CI_AS NULL,
    [URUN] nvarchar(50) COLLATE Latin1_General_CI_AS NULL,
    [MOBIL] nvarchar(50) COLLATE Latin1_General_CI_AS NULL,
    [YAZILIM_TOPLAMAG] decimal(10,3) NULL,
    [TAMAMLANMA_OARANI] decimal(10,3) NULL,
    [MUSTERI_SORUMLUSU] varchar(1) NOT NULL,
    [SATIS_SORUMLUSU] varchar(1) NOT NULL,
    [YARATICI] nvarchar(256) COLLATE Latin1_General_CI_AS NULL,
    [PLANLANAN_PYUAT] datetime NOT NULL,
    [GERCEKLESEN_PYUAT] datetime NOT NULL,
    [PLANLAN_CANLITESLIM] datetime NOT NULL,
    [GERCEKLESEN_CANLITESLIM] datetime NOT NULL
);
GO


CREATE TABLE [TALEPCONNECT] (
    [TFSNO] float NULL,
    [MADDEBASLIK] nvarchar(255) NULL,
    [MADDEDURUM] nvarchar(255) NULL,
    [ACILMATARIHI] nvarchar(255) NULL,
    [ACANKULLANICI] nvarchar(255) NULL,
    [PROJE] nvarchar(255) NULL,
    [URUN] nvarchar(255) NULL,
    [MUSTERI_SORUMLUSU] nvarchar(255) NULL,
    [SATIS_SORUMLUSU] nvarchar(255) NULL,
    [YARATICI] nvarchar(255) NULL,
    [ConnectDurum] nvarchar(255) NULL
);
GO


CREATE TABLE [TBL_KULLANICI] (
    [LNGKOD] int NOT NULL IDENTITY,
    [LNGIDENTITYKOD] int NULL,
    [TXTADSOYAD] varchar(256) NULL,
    [TXTFIRMAADI] varchar(512) NULL,
    [LNGKULLANICITIPI] int NULL,
    [LNGORTAKFIRMAKOD] int NULL,
    [TXTEMAIL] varchar(256) NULL,
    [LNGYETKIGRUPKOD] int NULL,
    [LNGKULLANICITIP] int NULL DEFAULT 3,
    CONSTRAINT [PK_TBL_KULLANICI] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_KULLANICI_FIRMA] (
    [LNGKOD] int NOT NULL IDENTITY,
    [LNGKULLANICIKOD] int NOT NULL,
    [LNGFIRMAKOD] int NOT NULL,
    CONSTRAINT [PK_TBL_KULLANICI_FIRMA] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_LOG] (
    [LNGKOD] int NOT NULL IDENTITY,
    [TXTTUR] varchar(32) NULL,
    [TXTACIKLAMA] varchar(512) NULL,
    [TRHTARIH] datetime NULL,
    CONSTRAINT [PK_TBL_LOG] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_MENU] (
    [LNGKOD] int NOT NULL IDENTITY,
    [TXTBASLIK] nvarchar(100) NOT NULL,
    [TXTLINK] nvarchar(255) NULL,
    [TXTICON] nvarchar(50) NULL,
    [LNGPARENTKOD] int NULL,
    [INTORDER] int NULL DEFAULT 0,
    [AKTIF] bit NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK_TBL_MENU] PRIMARY KEY ([LNGKOD]),
    CONSTRAINT [FK_MENU_PARENT] FOREIGN KEY ([LNGPARENTKOD]) REFERENCES [TBL_MENU] ([LNGKOD])
);
GO


CREATE TABLE [TBL_MENU_YETKI_GRUBU] (
    [LNGKOD] int NOT NULL IDENTITY,
    [TXTGRUPADI] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_TBL_MENU_YETKI_GRUBU] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_N4BISSUES] (
    [LNGKOD] int NOT NULL IDENTITY,
    [TXTBILDIRIMBASLIK] varchar(256) NULL,
    [TXTBILDIRIMACIKALAMA] varchar(max) NULL,
    [CustomerEmail] varchar(512) NULL,
    [CategoryID] int NULL,
    [ContactMethodID] int NOT NULL DEFAULT 4648,
    [IssueTypeID] int NULL,
    [IssueID] int NULL,
    [DURUM] int NULL,
    CONSTRAINT [PK_TBL_N4BISSUES] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_POS_ISLEM] (
    [LNGKOD] bigint NOT NULL IDENTITY,
    [TXTMASKKARTNO] nvarchar(128) NULL,
    [TXTADSOYAD] nvarchar(256) NULL,
    [TXTFIRMAAD] nvarchar(512) NULL,
    [LNGORTAKFIRMAKOD] int NULL,
    [TXTACIKLAMA] nvarchar(512) NULL,
    [TUTAR] money NULL,
    [GUVENLIKTIPI] varchar(16) NULL,
    [ONAY1] nvarchar(50) NULL,
    [ONAY2] nvarchar(50) NULL,
    [POS_ISLEMID] nvarchar(50) NULL,
    [POS_KOM_ORAN] money NULL,
    [POS_SONUC] nvarchar(50) NULL,
    [POS_SONUCSTR] nvarchar(256) NULL,
    [POS_ISLEM_TARIHI] datetime NULL,
    [TURKPOS_RETVAL_Banka_Sonuc_kod] nvarchar(50) NULL,
    [TURKPOS_RETVAL_Dekont_ID] nvarchar(50) NULL,
    [TURKPOS_RETVAL_Ext_Data] nvarchar(128) NULL,
    [TURKPOS_RETVAL_GUID] nvarchar(50) NULL,
    [TURKPOS_RETVAL_Hash] nvarchar(50) NULL,
    [TURKPOS_RETVAL_Islem_ID] nvarchar(50) NULL,
    [TURKPOS_RETVAL_Islem_Tarih] datetime NULL,
    [TURKPOS_RETVAL_Odeme_Tutari] money NULL,
    [TURKPOS_RETVAL_SiparisID] nvarchar(50) NULL,
    [TURKPOS_RETVAL_Sonuc] nvarchar(50) NULL,
    [TURKPOS_RETVAL_Sonuc_Str] nvarchar(50) NULL,
    [TURKPOS_RETVAL_Tahsilat_Tutari] money NULL,
    [TRH_CREATE_DATE] datetime NULL,
    [TRH_UPDATE_DATE] datetime NULL,
    [YUKLEMEYAPILDI] nvarchar(50) NULL,
    [YUKLEMETARIHI] datetime NULL,
    [LOGACIKLAMA] nvarchar(512) NULL,
    [EMAIL] varchar(128) NULL,
    [FATURANO] varchar(512) NULL,
    CONSTRAINT [PK_TBL_POS_ISLEM] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_SISTEM_LOGs] (
    [LNGKOD] int NOT NULL IDENTITY,
    [TXTKULLANICIADI] nvarchar(100) NULL,
    [LNGKULLANICIKOD] int NULL,
    [TXTISLEM] nvarchar(50) NULL,
    [TXTDETAY] nvarchar(max) NULL,
    [TXTIP] nvarchar(50) NULL,
    [TXTMODUL] nvarchar(50) NULL,
    [TRHKAYIT] datetime2 NULL,
    CONSTRAINT [PK_TBL_SISTEM_LOGs] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_TALEP] (
    [LNGKOD] int NOT NULL IDENTITY,
    [LNGPROJEKOD] int NULL,
    [TXTTALEPBASLIK] nvarchar(max) NULL,
    [TXTTALEPACIKLAMA] nvarchar(max) NULL,
    [LNGTFSNO] int NULL,
    [LNGVARUNAKOD] int NULL,
    [BYTDURUM] nchar(10) NULL,
    [DEC_EFOR] decimal(18,2) NULL,
    [TXT_SORUMLULAR] varchar(max) NULL,
    [TXT_PO] nvarchar(50) NULL,
    [TRHKAYIT] datetime NULL DEFAULT ((getdate())),
    [INT_ANKET_PUAN] int NULL,
    [TXT_ANKET_NOT] nvarchar(500) NULL,
    CONSTRAINT [PK_TBL_TALEP] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_TALEP_AKISDURUMLARI] (
    [LNGKOD] int NOT NULL IDENTITY,
    [TXTDURUMADI] varchar(128) NULL,
    CONSTRAINT [PK_TBL_TALEP_AKISDURUMLARI] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_VARUNA_SIPARIS] (
    [LNGKOD] int NOT NULL IDENTITY,
    [CreateOrderDate] datetime NULL,
    [OrderId] varchar(64) NULL,
    [InvoiceDate] datetime NULL,
    [PaymentType] varchar(64) NULL,
    [PaymentTypeTime] varchar(64) NULL,
    [OrderStatus] varchar(64) NULL,
    [QuoteId] varchar(64) NULL,
    [AccountId] varchar(64) NULL,
    [ProposalOwnerId] varchar(64) NULL,
    [SubTotalDiscount] money NULL,
    [CompanyId] varchar(64) NULL,
    [IsEligibleForNetsisIntegration] bit NULL,
    [SAPOutReferenceCode] varchar(64) NULL,
    [DistributionChannelSapId] varchar(64) NULL,
    [DivisionSapId] varchar(64) NULL,
    [SalesDocumentTypeSapId] varchar(64) NULL,
    [SalesOrganizationSapId] varchar(64) NULL,
    [CrmSalesOfficeSapId] varchar(64) NULL,
    [SalesGroupSapId] varchar(64) NULL,
    [IsEligibleForSapIntegration] bit NULL,
    [CrmOrderNotes] varchar(512) NULL,
    [SerialNumber] varchar(64) NULL,
    [CreatedOn] datetime NULL,
    [CreatedBy] varchar(128) NULL,
    [ModifiedOn] datetime NULL,
    [ModifiedBy] varchar(128) NULL,
    [TotalNetAmount] money NULL,
    [TotalAmountWithTax] money NULL,
    [TotalProfitAmount] money NULL,
    [AccountTitle] varchar(512) NULL,
    [AccountSAPOutReferenceCode] varchar(64) NULL,
    CONSTRAINT [PK_TBL_VARUNA_SIPARIS] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_VARUNA_SIPARIS_20260121] (
    [LNGKOD] int NOT NULL IDENTITY,
    [CreateOrderDate] datetime NULL,
    [OrderId] varchar(64) NULL,
    [InvoiceDate] datetime NULL,
    [PaymentType] varchar(64) NULL,
    [PaymentTypeTime] varchar(64) NULL,
    [OrderStatus] varchar(64) NULL,
    [QuoteId] varchar(64) NULL,
    [AccountId] varchar(64) NULL,
    [ProposalOwnerId] varchar(64) NULL,
    [SubTotalDiscount] money NULL,
    [CompanyId] varchar(64) NULL,
    [IsEligibleForNetsisIntegration] bit NULL,
    [SAPOutReferenceCode] varchar(64) NULL,
    [DistributionChannelSapId] varchar(64) NULL,
    [DivisionSapId] varchar(64) NULL,
    [SalesDocumentTypeSapId] varchar(64) NULL,
    [SalesOrganizationSapId] varchar(64) NULL,
    [CrmSalesOfficeSapId] varchar(64) NULL,
    [SalesGroupSapId] varchar(64) NULL,
    [IsEligibleForSapIntegration] bit NULL,
    [CrmOrderNotes] varchar(512) NULL,
    [SerialNumber] varchar(64) NULL,
    [CreatedOn] datetime NULL,
    [CreatedBy] varchar(128) NULL,
    [ModifiedOn] datetime NULL,
    [ModifiedBy] varchar(128) NULL,
    [TotalNetAmount] money NULL,
    [TotalAmountWithTax] money NULL,
    [TotalProfitAmount] money NULL,
    [AccountTitle] varchar(512) NULL,
    [AccountSAPOutReferenceCode] varchar(64) NULL
);
GO


CREATE TABLE [TBL_VARUNA_SIPARIS_URUNLERI] (
    [LNGKOD] int NOT NULL IDENTITY,
    [DeliveryTime] datetime NULL,
    [TransactionDate] datetime NULL,
    [PRODUCTSID] varchar(64) NULL,
    [CrmOrderId] varchar(64) NULL,
    [StockId] varchar(64) NULL,
    [Quantity] money NULL,
    [LineDiscountRate] money NULL,
    [StockUnitType] varchar(64) NULL,
    [Tax] money NULL,
    [PYPSapId] varchar(64) NULL,
    [CreatedOn] datetime NULL,
    [ModifiedOn] datetime NULL,
    [UnitPrice] money NULL,
    [Total] money NULL,
    [NetLineTotalWithTax] money NULL,
    [ProductName] varchar(512) NULL,
    [StockCode] varchar(128) NULL,
    [ItemNo] varchar(32) NULL,
    CONSTRAINT [PK_TBL_VARUNA_SIPARIS_URUNLERI] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_VARUNA_SIPARIS_URUNLERI_20260121] (
    [LNGKOD] int NOT NULL IDENTITY,
    [DeliveryTime] datetime NULL,
    [TransactionDate] datetime NULL,
    [PRODUCTSID] varchar(64) NULL,
    [CrmOrderId] varchar(64) NULL,
    [StockId] varchar(64) NULL,
    [Quantity] money NULL,
    [LineDiscountRate] money NULL,
    [StockUnitType] varchar(64) NULL,
    [Tax] money NULL,
    [PYPSapId] varchar(64) NULL,
    [CreatedOn] datetime NULL,
    [ModifiedOn] datetime NULL,
    [UnitPrice] money NULL,
    [Total] money NULL,
    [NetLineTotalWithTax] money NULL,
    [ProductName] varchar(512) NULL,
    [StockCode] varchar(128) NULL
);
GO


CREATE TABLE [TBL_VARUNA_SOZLESME] (
    [LNGKOD] int NOT NULL IDENTITY,
    [Id] uniqueidentifier NULL,
    [StartDate] datetime NULL,
    [FinishDate] datetime NULL,
    [RenewalDate] datetime NULL,
    [ContractNo] nvarchar(50) NULL,
    [ContractType] nvarchar(50) NULL,
    [ContractStatus] nvarchar(100) NULL,
    [AccountId] uniqueidentifier NULL,
    [AccountCode] nvarchar(50) NULL,
    [AccountTitle] nvarchar(250) NULL,
    [SalesRepresentativeId] uniqueidentifier NULL,
    [CompanyId] uniqueidentifier NULL,
    [ProductId] uniqueidentifier NULL,
    [InvoiceNumber] int NULL,
    [InvoiceStatusId] uniqueidentifier NULL,
    [InvoiceDueDate] int NULL,
    [StampTaxRate] decimal(18,8) NULL,
    [StampTaxAmount] decimal(18,2) NULL,
    [IsLateInterestApply] bit NULL,
    [LateInterestContractYear] int NULL,
    [IsAutoExtending] bit NULL,
    [TotalAmountCurrency] nvarchar(10) NULL,
    [TotalAmount] decimal(18,2) NULL,
    [TotalAmountLocalCurrency] nvarchar(10) NULL,
    [TotalAmountLocal] decimal(18,2) NULL,
    [RemainingBalanceCurrency] nvarchar(10) NULL,
    [RemainingBalance] decimal(18,2) NULL,
    [ContractUrl] nvarchar(500) NULL,
    [ApprovalCorrelationId] uniqueidentifier NULL,
    [ApprovalRequestedBy] nvarchar(150) NULL,
    [ApprovalRequestedOn] datetime2 NULL,
    [ApprovalAssignTo] nvarchar(150) NULL,
    [ApprovalRespondedOn] datetime2 NULL,
    [ApprovalState] nvarchar(50) NULL,
    [ApprovalResponse] nvarchar(250) NULL,
    [CreatedOn] datetime2 NULL,
    [CreatedBy] nvarchar(150) NULL,
    [CreatedPlatform] nvarchar(50) NULL,
    [ModifiedOn] datetime2 NULL,
    [ModifiedBy] nvarchar(150) NULL,
    [ModifiedPlatform] nvarchar(50) NULL,
    [DeletedOn] datetime2 NULL,
    [DeletedBy] nvarchar(150) NULL,
    [Tags] nvarchar(250) NULL,
    [ContractName] nvarchar(1024) NULL,
    CONSTRAINT [PK__TBL_VARU__E133217F602D71EF] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_VARUNA_SOZLESME_DOSYALAR] (
    [LNGKOD] int NOT NULL IDENTITY,
    [ContractId] nvarchar(512) NULL,
    [FileName] nvarchar(max) NULL,
    [FileBase64] nvarchar(max) NULL,
    [FileContentType] nvarchar(max) NULL,
    [FileExtension] nvarchar(max) NULL,
    CONSTRAINT [PK_TBL_VARUNA_SOZLESME_DOSYALAR] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_VARUNA_SOZLESME_INVENTORYITEM] (
    [LNGKOD] int NOT NULL IDENTITY,
    [Id] nvarchar(512) NULL,
    [StartDate] datetime NULL,
    [FinishDate] datetime NULL,
    [InvInstalledDate] datetime NULL,
    [InvPurchaseDate] datetime NULL,
    [Status] nvarchar(50) NULL,
    [StatusCrm] nvarchar(50) NULL,
    [FinancialProductType] nvarchar(50) NULL,
    [PFApplicaitonType] nvarchar(50) NULL,
    [AccountId] nvarchar(512) NULL,
    [StockId] nvarchar(512) NULL,
    [ContactId] nvarchar(512) NULL,
    [ContractId] nvarchar(512) NULL,
    [CompanyId] nvarchar(512) NULL,
    [ProductGroupId] nvarchar(512) NULL,
    [ProductSubGroupId] nvarchar(512) NULL,
    [QuoteId] nvarchar(512) NULL,
    [CrmOrderID] nvarchar(512) NULL,
    [Amount] int NULL,
    [Domain] nvarchar(150) NULL,
    [PosAccountNo] nvarchar(100) NULL,
    [CardNo] nvarchar(100) NULL,
    [InvSerialId] nvarchar(512) NULL,
    [InvOutOfWarehouseSerial] bit NULL,
    [InvOutOfWarehouseSerialCode] nvarchar(100) NULL,
    [IsSentToTsm] bit NULL,
    [PriceCurrency] nvarchar(10) NULL,
    [PriceAmount] decimal(18,4) NULL,
    [VatCurrency] nvarchar(10) NULL,
    [VatAmount] decimal(18,4) NULL,
    [TotalListPriceCurrency] nvarchar(10) NULL,
    [TotalListPriceAmount] decimal(18,4) NULL,
    [TotalPackagePriceCurrency] nvarchar(10) NULL,
    [TotalPackagePriceAmount] decimal(18,4) NULL,
    [TotalPackageVatCurrency] nvarchar(10) NULL,
    [TotalPackageVatAmount] decimal(18,4) NULL,
    [InstallCountryCode] nvarchar(10) NULL,
    [InstallState] nvarchar(100) NULL,
    [InstallProvince] nvarchar(100) NULL,
    [InstallDistrict] nvarchar(100) NULL,
    [InstallPlaceName] nvarchar(150) NULL,
    [InstallSubdivision1] nvarchar(100) NULL,
    [InstallSubdivision2] nvarchar(100) NULL,
    [InstallSubdivision3] nvarchar(100) NULL,
    [InstallSubdivision4] nvarchar(100) NULL,
    [InstallPostalCode] nvarchar(20) NULL,
    [InstallAddress] nvarchar(500) NULL,
    [InstallCity] nvarchar(100) NULL,
    [InstallLongitude] float NULL,
    [InstallLatitude] float NULL,
    [StockCode] nvarchar(50) NULL,
    [StockName] nvarchar(250) NULL,
    [TPOutReferenceCode] nvarchar(50) NULL,
    [SAPOutReferenceCode] nvarchar(50) NULL,
    [ApprovalCorrelationId] nvarchar(512) NULL,
    [ApprovalRequestedBy] nvarchar(150) NULL,
    [ApprovalRequestedOn] datetime2 NULL,
    [ApprovalAssignTo] nvarchar(150) NULL,
    [ApprovalRespondedOn] datetime2 NULL,
    [ApprovalState] nvarchar(50) NULL,
    [ApprovalResponse] nvarchar(250) NULL,
    [CreatedOn] datetime2 NULL,
    [CreatedBy] nvarchar(150) NULL,
    [CreatedPlatform] nvarchar(50) NULL,
    [ModifiedOn] datetime2 NULL,
    [ModifiedBy] nvarchar(150) NULL,
    [ModifiedPlatform] nvarchar(50) NULL,
    [DeletedOn] datetime2 NULL,
    [DeletedBy] nvarchar(150) NULL,
    [Tags] nvarchar(250) NULL,
    CONSTRAINT [PK__TBL_VARU__E133217F29AC7FFB] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_VARUNA_STOKKOD_GRUP] (
    [LNGKOD] int NOT NULL IDENTITY,
    [STOCKKOD] varchar(50) NULL,
    [TXTGRUPAD] varchar(50) NULL,
    CONSTRAINT [PK_TBL_VARUNA_STOKKOD_GRUP] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_VARUNA_TEKLIF] (
    [Id] uniqueidentifier NOT NULL,
    [DeliveryDate] datetime NULL,
    [ExpirationDate] datetime NULL,
    [FirstCreatedDate] datetime NULL,
    [FirstReleaseDate] datetime NULL,
    [RevisedDate] datetime NULL,
    [ServiceFinishDate] datetime NULL,
    [ServiceStartDate] datetime NULL,
    [PaymentType] nvarchar(50) NULL,
    [PaymentTypeTime] nvarchar(50) NULL,
    [Status] nvarchar(50) NULL,
    [DeliveryType] nvarchar(50) NULL,
    [DeliveryTypeTime] nvarchar(50) NULL,
    [RevStatus] nvarchar(50) NULL,
    [SubTotalAlternativeCurrency] decimal(18,4) NULL,
    [SubTotalDiscountType] nvarchar(50) NULL,
    [QuoteApprovalProcessStatus] nvarchar(50) NULL,
    [QuoteType] nvarchar(50) NULL,
    [OpportunityId] uniqueidentifier NULL,
    [RevisionId] uniqueidentifier NULL,
    [Number] nvarchar(50) NULL,
    [Name] nvarchar(255) NULL,
    [SubTotalDiscount] decimal(18,4) NULL,
    [WarehouseId] uniqueidentifier NULL,
    [ProposalOwnerId] uniqueidentifier NULL,
    [AddressIdentifier] nvarchar(100) NULL,
    [DeliveryTime] nvarchar(50) NULL,
    [CustomerOrderNumber] nvarchar(512) NULL,
    [PaymentTime] nvarchar(50) NULL,
    [PersonId] uniqueidentifier NULL,
    [SpecialCodeId] uniqueidentifier NULL,
    [AccountId] uniqueidentifier NULL,
    [Description] nvarchar(max) NULL,
    [RevNo] int NULL,
    [AlternativeCurrencyRate] decimal(18,6) NULL,
    [IsVATExempt] bit NULL,
    [TermsAndConditions] nvarchar(max) NULL,
    [ProductsAndServices] nvarchar(max) NULL,
    [ReferenceCode] nvarchar(100) NULL,
    [TransferWithForeignCurrency] bit NULL,
    [ContactId] uniqueidentifier NULL,
    [FirstCreatedByName] nvarchar(255) NULL,
    [TeamId] uniqueidentifier NULL,
    [TeamCreatedById] uniqueidentifier NULL,
    [SubTotalDiscountAmount] decimal(18,4) NULL,
    [InRefCode] nvarchar(100) NULL,
    [CompanyId] uniqueidentifier NULL,
    [TotalDiscountRate] decimal(18,4) NULL,
    [CRMRevNo] varchar(50) NULL,
    [PublicationSource] nvarchar(100) NULL,
    [TermsAndConditions2] nvarchar(max) NULL,
    [ProductsAndServices2] nvarchar(max) NULL,
    [StockId] uniqueidentifier NULL,
    [ItemNo] nvarchar(512) NULL,
    [CrmOrderId] uniqueidentifier NULL,
    [OrderWillBeCreate] bit NULL,
    [OrderOwnerWillBeChanged] bit NULL,
    [TPOutReferenceCode] nvarchar(100) NULL,
    [CreatedOn] datetime2 NULL,
    [CreatedBy] nvarchar(255) NULL,
    [ModifiedOn] datetime2 NULL,
    [ModifiedBy] nvarchar(255) NULL,
    [DeletedOn] datetime2 NULL,
    [DeletedBy] nvarchar(255) NULL,
    [Tags] nvarchar(max) NULL,
    [ApprovalCorrelationId] uniqueidentifier NULL,
    [ApprovalRequestedBy] uniqueidentifier NULL,
    [ApprovalRequestedOn] datetime2 NULL,
    [ApprovalAssignTo] uniqueidentifier NULL,
    [ApprovalRespondedOn] datetime2 NULL,
    [ApprovalState] nvarchar(50) NULL,
    [ApprovalResponse] nvarchar(255) NULL,
    [CreatedPlatform] nvarchar(50) NULL,
    [ModifiedPlatform] nvarchar(50) NULL,
    [NetSubTotalLocalCurrency_Currency] nvarchar(10) NULL,
    [NetSubTotalLocalCurrency_Amount] decimal(18,8) NULL,
    [NetSubTotalLocalCurrency_HasValue] bit NULL,
    [TotalNetAmountLocalCurrency_Currency] nvarchar(10) NULL,
    [TotalNetAmountLocalCurrency_Amount] decimal(18,8) NULL,
    [TotalAmountWithTaxLocalCurrency_Currency] nvarchar(10) NULL,
    [TotalAmountWithTaxLocalCurrency_Amount] decimal(18,8) NULL,
    [TotalProfitAmount_Currency] nvarchar(10) NULL,
    [TotalProfitAmount_Amount] decimal(18,8) NULL,
    [NetSubTotalAlternativeCurrency_Currency] nvarchar(10) NULL,
    [NetSubTotalAlternativeCurrency_Amount] decimal(18,8) NULL,
    [TotalNetAmountAlternativeCurrency_Currency] nvarchar(10) NULL,
    [TotalNetAmountAlternativeCurrency_Amount] decimal(18,8) NULL,
    [TotalAmountWithTaxAlternativeCurrency_Currency] nvarchar(10) NULL,
    [TotalAmountWithTaxAlternativeCurrency_Amount] decimal(18,8) NULL,
    [TotalProfitAmountAlternativeCurrency_Currency] nvarchar(10) NULL,
    [TotalProfitAmountAlternativeCurrency_Amount] decimal(18,8) NULL,
    [Account_Title] nvarchar(255) NULL,
    [Account_SAPOutReferenceCode] nvarchar(100) NULL,
    CONSTRAINT [PK__TBL_VARU__3214EC070CCD6DBA] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [TBL_VARUNA_TEKLIF_URUNLERI] (
    [Id] uniqueidentifier NOT NULL,
    [DeliveryTime] datetime NULL,
    [TransactionDate] datetime NULL,
    [QuoteId] uniqueidentifier NULL,
    [StockId] uniqueidentifier NULL,
    [LineDiscountType] nvarchar(50) NULL,
    [Quantity] decimal(18,4) NULL,
    [LineDiscountRate] decimal(18,4) NULL,
    [Description] nvarchar(max) NULL,
    [StockUnitTypeIdentifier] uniqueidentifier NULL,
    [StockUnitType] nvarchar(50) NULL,
    [Tax] decimal(18,4) NULL,
    [ProfitRate] decimal(18,4) NULL,
    [CurrencyRate] decimal(18,6) NULL,
    [ComissionRate] decimal(18,4) NULL,
    [ItemNo] nvarchar(50) NULL,
    [PYPSapId] nvarchar(100) NULL,
    [StorageLocationSapId] nvarchar(100) NULL,
    [ProductionLocationSapId] nvarchar(100) NULL,
    [UnorderedQuantity] decimal(18,4) NULL,
    [CreatedOn] datetime2 NULL,
    [CreatedBy] nvarchar(255) NULL,
    [ModifiedOn] datetime2 NULL,
    [ModifiedBy] nvarchar(255) NULL,
    [DeletedOn] datetime2 NULL,
    [DeletedBy] nvarchar(255) NULL,
    [Tags] nvarchar(max) NULL,
    [ApprovalCorrelationId] uniqueidentifier NULL,
    [ApprovalRequestedBy] uniqueidentifier NULL,
    [ApprovalRequestedOn] datetime2 NULL,
    [ApprovalAssignTo] uniqueidentifier NULL,
    [ApprovalRespondedOn] datetime2 NULL,
    [ApprovalState] nvarchar(50) NULL,
    [ApprovalResponse] nvarchar(255) NULL,
    [CreatedPlatform] nvarchar(50) NULL,
    [ModifiedPlatform] nvarchar(50) NULL,
    [LineDiscountAmount_Currency] nvarchar(10) NULL,
    [LineDiscountAmount_Amount] decimal(18,8) NULL,
    [UnitPrice_Currency] nvarchar(10) NULL,
    [UnitPrice_Amount] decimal(18,8) NULL,
    [PurchasingPrice_Currency] nvarchar(10) NULL,
    [PurchasingPrice_Amount] decimal(18,8) NULL,
    [Total_Currency] nvarchar(10) NULL,
    [Total_Amount] decimal(18,8) NULL,
    [NetLineSubTotal_Currency] nvarchar(10) NULL,
    [NetLineSubTotal_Amount] decimal(18,8) NULL,
    [TotalProfitAmountLocal_Currency] nvarchar(10) NULL,
    [TotalProfitAmountLocal_Amount] decimal(18,8) NULL,
    [UnitProfitAmountLocal_Currency] nvarchar(10) NULL,
    [UnitProfitAmountLocal_Amount] decimal(18,8) NULL,
    [NetLineTotalAmount_Currency] nvarchar(10) NULL,
    [NetLineTotalAmount_Amount] decimal(18,8) NULL,
    [NetLineTotalWithTax_Currency] nvarchar(10) NULL,
    [NetLineTotalWithTax_Amount] decimal(18,8) NULL,
    [NetLineTotalWithTaxLocal_Currency] nvarchar(10) NULL,
    [NetLineTotalWithTaxLocal_Amount] decimal(18,8) NULL,
    [NetLineSubTotalLocal_Currency] nvarchar(10) NULL,
    [NetLineSubTotalLocal_Amount] decimal(18,8) NULL,
    [NetLineTotalAmountLocal_Currency] nvarchar(10) NULL,
    [NetLineTotalAmountLocal_Amount] decimal(18,8) NULL,
    [ProfitAfterSubtotalDiscountLocal_Currency] nvarchar(10) NULL,
    [ProfitAfterSubtotalDiscountLocal_Amount] decimal(18,8) NULL,
    [StockType] nvarchar(512) NULL,
    [StockCode] nvarchar(256) NULL,
    [StockName] nvarchar(512) NULL,
    [StockSalesVatValue] decimal(18,8) NULL,
    CONSTRAINT [PK__TBL_VARU__3214EC0708ED4C53] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [TBL_VARUNA_URUN_GRUPLAMA] (
    [LNGKOD] int NOT NULL IDENTITY,
    [TXTURUNMASK] varchar(50) NULL,
    [TXTKOD] varchar(50) NULL,
    [TXTURUNGRUP] varchar(128) NULL,
    CONSTRAINT [PK_TBL_VARUNA_URUN_GRUPLAMA] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBL_ZABBIX_HOST_LIST] (
    [LNGKOD] int NOT NULL IDENTITY,
    [HOSTID] int NULL,
    [HOST] varchar(512) NULL,
    [NAME] varchar(512) NULL,
    [IP] varchar(50) NULL,
    [LNGORTAKPROJEKOD] int NULL,
    [TXTORTAKPROJEISIM] varchar(256) NULL,
    [TRHGDT] datetime NULL,
    [ACIKLAMA] nvarchar(100) NULL,
    CONSTRAINT [PK_TBL_ZABBIX_HOST_LIST] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [TBLPARAMETRE] (
    [LNGKOD] int NOT NULL IDENTITY,
    [TXTPARAMETRE] varchar(256) NULL,
    [TXTDEGER] varchar(1024) NULL,
    [BYTGRUP] smallint NULL,
    CONSTRAINT [PK_TBLPARAMETRE] PRIMARY KEY ([LNGKOD])
);
GO


CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] int NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] int NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserRoles] (
    [UserId] int NOT NULL,
    [RoleId] int NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserTokens] (
    [UserId] int NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [TBL_FINANS_ONAY] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] nvarchar(50) NOT NULL,
    [PONumber] nvarchar(100) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] int NULL,
    [IsRevoked] bit NOT NULL,
    [RevokedBy] int NULL,
    [RevokedDate] datetime2 NULL,
    CONSTRAINT [PK_TBL_FINANS_ONAY] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TBL_FINANS_ONAY_TBL_KULLANICI_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [TBL_KULLANICI] ([LNGKOD]),
    CONSTRAINT [FK_TBL_FINANS_ONAY_TBL_KULLANICI_RevokedBy] FOREIGN KEY ([RevokedBy]) REFERENCES [TBL_KULLANICI] ([LNGKOD])
);
GO


CREATE TABLE [TBL_MENU_YETKI_GRUBU_MENU] (
    [LNGKOD] int NOT NULL IDENTITY,
    [LNGGRUPKOD] int NOT NULL,
    [LNGMENUKOD] int NOT NULL,
    CONSTRAINT [PK_TBL_MENU_YETKI_GRUBU_MENU] PRIMARY KEY ([LNGKOD]),
    CONSTRAINT [FK_GROUP] FOREIGN KEY ([LNGGRUPKOD]) REFERENCES [TBL_MENU_YETKI_GRUBU] ([LNGKOD]),
    CONSTRAINT [FK_MENU] FOREIGN KEY ([LNGMENUKOD]) REFERENCES [TBL_MENU] ([LNGKOD])
);
GO


CREATE TABLE [TBL_N4BISSSEFILES] (
    [LNGKOD] int NOT NULL IDENTITY,
    [LNGTBLISSUEKOD] int NULL,
    [FileName] nvarchar(max) NULL,
    [FileBase64] nvarchar(max) NULL,
    [FileContentType] nvarchar(max) NULL,
    [FileExtension] nvarchar(max) NULL,
    CONSTRAINT [PK_TBL_N4BISSSEFILES] PRIMARY KEY ([LNGKOD]),
    CONSTRAINT [FK_TBL_N4BISSSEFILES_TBL_N4BISSUES] FOREIGN KEY ([LNGTBLISSUEKOD]) REFERENCES [TBL_N4BISSUES] ([LNGKOD])
);
GO


CREATE TABLE [TBL_TALEP_FILES] (
    [LNGKOD] int NOT NULL IDENTITY,
    [LNGTALEPKOD] int NULL,
    [FileName] nvarchar(max) NULL,
    [FileBase64] nvarchar(max) NULL,
    [FileContentType] nvarchar(max) NULL,
    CONSTRAINT [PK_TBL_TALEP_FILES] PRIMARY KEY ([LNGKOD]),
    CONSTRAINT [FK_TBL_TALEP_FILES_TBL_TALEP] FOREIGN KEY ([LNGTALEPKOD]) REFERENCES [TBL_TALEP] ([LNGKOD])
);
GO


CREATE TABLE [TBL_TALEP_NOTLAR] (
    [LNGKOD] int NOT NULL IDENTITY,
    [LNGTALEPKOD] int NULL,
    [TXTNOT] varchar(max) NULL,
    [LNGKULLANICIKOD] int NULL,
    [BYTDURUM] int NULL,
    CONSTRAINT [PK_TBL_TALEP_NOTLAR] PRIMARY KEY ([LNGKOD]),
    CONSTRAINT [FK_TBL_TALEP_NOTLAR_TBL_TALEP] FOREIGN KEY ([LNGTALEPKOD]) REFERENCES [TBL_TALEP] ([LNGKOD])
);
GO


CREATE TABLE [TBL_TALEP_AKIS_LOG] (
    [LNGKOD] int NOT NULL IDENTITY,
    [LNGTFSNO] int NULL,
    [LNGSIRA] int NULL,
    [LNGDURUMKOD] int NULL,
    [TRHDURUMBASLANGIC] datetime NULL,
    [TRHDURUMONAY] datetime NULL,
    [TRHDURUMGERIALMA] datetime NULL,
    [LNGBASLANGICKULLANICI] int NULL,
    [LNGONAYKULLANICI] int NULL,
    [LNGGERIALKULLANICI] int NULL,
    [LNGTALEPKOD] int NULL,
    CONSTRAINT [PK_TBL_TALEP_AKIS_LOG] PRIMARY KEY ([LNGKOD]),
    CONSTRAINT [FK_TBL_TALEP_AKIS_LOG_TBL_TALEP_AKISDURUMLARI] FOREIGN KEY ([LNGDURUMKOD]) REFERENCES [TBL_TALEP_AKISDURUMLARI] ([LNGKOD])
);
GO


CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO


CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE ([NormalizedName] IS NOT NULL);
GO


CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO


CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO


CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
GO


CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
GO


CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE ([NormalizedUserName] IS NOT NULL);
GO


CREATE INDEX [IX_TBL_FINANS_ONAY_CreatedBy] ON [TBL_FINANS_ONAY] ([CreatedBy]);
GO


CREATE INDEX [IX_TBL_FINANS_ONAY_RevokedBy] ON [TBL_FINANS_ONAY] ([RevokedBy]);
GO


CREATE INDEX [IX_TBL_MENU_LNGPARENTKOD] ON [TBL_MENU] ([LNGPARENTKOD]);
GO


CREATE INDEX [IX_TBL_MENU_YETKI_GRUBU_MENU_LNGGRUPKOD] ON [TBL_MENU_YETKI_GRUBU_MENU] ([LNGGRUPKOD]);
GO


CREATE INDEX [IX_TBL_MENU_YETKI_GRUBU_MENU_LNGMENUKOD] ON [TBL_MENU_YETKI_GRUBU_MENU] ([LNGMENUKOD]);
GO


CREATE INDEX [IX_TBL_N4BISSSEFILES_LNGTBLISSUEKOD] ON [TBL_N4BISSSEFILES] ([LNGTBLISSUEKOD]);
GO


CREATE INDEX [IX_TBL_TALEP_AKIS_LOG_LNGDURUMKOD] ON [TBL_TALEP_AKIS_LOG] ([LNGDURUMKOD]);
GO


CREATE INDEX [IX_TBL_TALEP_FILES_LNGTALEPKOD] ON [TBL_TALEP_FILES] ([LNGTALEPKOD]);
GO


CREATE INDEX [IX_TBL_TALEP_NOTLAR_LNGTALEPKOD] ON [TBL_TALEP_NOTLAR] ([LNGTALEPKOD]);
GO


