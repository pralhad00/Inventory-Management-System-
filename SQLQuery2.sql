USE InventoryManagementSystemDB;
GO

CREATE USER inventoryuser FOR LOGIN inventoryuser;
GO

ALTER ROLE db_owner ADD MEMBER inventoryuser;
GO