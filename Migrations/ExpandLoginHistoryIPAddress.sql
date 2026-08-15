-- Expand LoginHistory.IPAddress from NVARCHAR(20) to NVARCHAR(45).
-- NVARCHAR(20) holds IPv4 addresses (max 15 chars) but not full IPv6 addresses
-- (up to 39 chars, e.g. 2001:0db8:85a3:0000:8a2e:0370:7334).
-- Production servers commonly receive IPv6 connections; the short column caused a
-- string-truncation SqlException on the LoginHistory INSERT, surfacing as a 500
-- on every login attempt from an IPv6 client.  NVARCHAR(45) accommodates both.

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.LoginHistory')
      AND name = N'IPAddress'
      AND max_length < 90   -- 45 chars * 2 bytes/char for NVARCHAR
)
    ALTER TABLE [dbo].[LoginHistory]
        ALTER COLUMN [IPAddress] NVARCHAR(45) NULL;
