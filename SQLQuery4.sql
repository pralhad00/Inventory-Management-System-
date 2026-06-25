USE InventoryManagementSystemDB;
GO

CREATE TABLE Inventories (
    id INT IDENTITY(1,1) NOT NULL,
    name VARCHAR(100) NOT NULL,
    PRIMARY KEY (id)
);
GO

ALTER TABLE Inventory
ADD inventory_id INT NULL;
GO