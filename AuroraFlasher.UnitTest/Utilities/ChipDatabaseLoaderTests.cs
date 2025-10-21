using Microsoft.VisualStudio.TestTools.UnitTesting;
using AuroraFlasher.Utilities;
using AuroraFlasher.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuroraFlasher.UnitTest.Utilities
{
    [TestClass]
    public class ChipDatabaseLoaderTests
    {
        private string _testDirectory;
        private string _testXmlPath;

        [TestInitialize]
        public void Setup()
        {
            // Create temporary directory for test XML files
            _testDirectory = Path.Combine(Path.GetTempPath(), "AuroraFlasherTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _testXmlPath = Path.Combine(_testDirectory, "chiplist.xml");
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up test files
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region LoadDatabase Tests

        [TestMethod]
        public void LoadDatabase_ValidXmlFile_ReturnsChips()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <Winbond>
            <W25Q128FV id=""EF4018"" size=""16777216"" page=""256"" spicmd=""25"" />
            <W25Q80BW_1.8V id=""EF5014"" size=""1048576"" page=""256"" spicmd=""25"" />
        </Winbond>
        <Macronix>
            <MX25L3206E id=""C22016"" size=""4194304"" page=""256"" spicmd=""25"" />
        </Macronix>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.IsNotNull(chips);
            Assert.AreEqual(3, chips.Count);
            
            var w25q128 = chips.FirstOrDefault(c => c.Name == "W25Q128FV");
            Assert.IsNotNull(w25q128);
            Assert.AreEqual(ChipManufacturer.Winbond, w25q128.Manufacturer);
            Assert.AreEqual(ProtocolType.SPI, w25q128.ProtocolType);
            Assert.AreEqual(16777216, w25q128.Size);
            Assert.AreEqual(256, w25q128.PageSize);
            Assert.AreEqual(3300, w25q128.Voltage); // Default 3.3V
            Assert.AreEqual(0xEF, w25q128.ManufacturerId);
            Assert.AreEqual(0x4018, w25q128.DeviceId);
        }

        [TestMethod]
        public void LoadDatabase_ChipWith18VVoltage_ParsesVoltageCorrectly()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <Winbond>
            <W25Q80BW_1.8V id=""EF5014"" size=""1048576"" page=""256"" />
            <GD25LQ20C_1.8V id=""C84012"" size=""262144"" page=""256"" />
        </Winbond>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.AreEqual(2, chips.Count);
            Assert.IsTrue(chips.All(c => c.Voltage == 1800), "All chips should have 1.8V voltage");
        }

        [TestMethod]
        public void LoadDatabase_ChipWith25VVoltage_ParsesVoltageCorrectly()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <Macronix>
            <MX25U1001E_2.5V id=""C22811"" size=""131072"" page=""256"" />
        </Macronix>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.AreEqual(1, chips.Count);
            Assert.AreEqual(2500, chips[0].Voltage);
        }

        [TestMethod]
        public void LoadDatabase_ChipWith5VVoltage_ParsesVoltageCorrectly()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <Atmel>
            <AT25DF041A_5V id=""1F4401"" size=""524288"" page=""256"" />
        </Atmel>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.AreEqual(1, chips.Count);
            Assert.AreEqual(5000, chips[0].Voltage);
        }

        [TestMethod]
        public void LoadDatabase_ChipWithoutVoltageSpecified_DefaultsTo33V()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <Winbond>
            <W25Q128FV id=""EF4018"" size=""16777216"" page=""256"" />
        </Winbond>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.AreEqual(1, chips.Count);
            Assert.AreEqual(3300, chips[0].Voltage);
        }

        [TestMethod]
        public void LoadDatabase_MultipleProtocols_ParsesAllTypes()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <Winbond>
            <W25Q128FV id=""EF4018"" size=""16777216"" page=""256"" />
        </Winbond>
    </SPI>
    <I2C>
        <Atmel>
            <AT24C256 id=""A0"" size=""32768"" page=""64"" addrtype=""0"" />
        </Atmel>
    </I2C>
    <MicroWire>
        <Atmel>
            <AT93C46 id=""00"" size=""128"" page=""1"" addrbitlen=""7"" />
        </Atmel>
    </MicroWire>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.AreEqual(3, chips.Count);
            Assert.AreEqual(1, chips.Count(c => c.ProtocolType == ProtocolType.SPI));
            Assert.AreEqual(1, chips.Count(c => c.ProtocolType == ProtocolType.I2C));
            Assert.AreEqual(1, chips.Count(c => c.ProtocolType == ProtocolType.MicroWire));
        }

        [TestMethod]
        public void LoadDatabase_FileNotFound_ReturnsEmptyList()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.xml");

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(nonExistentPath);

            // Assert
            Assert.IsNotNull(chips);
            Assert.AreEqual(0, chips.Count);
        }

        [TestMethod]
        public void LoadDatabase_InvalidXml_ReturnsEmptyList()
        {
            // Arrange
            var invalidXml = "This is not valid XML content!";
            File.WriteAllText(_testXmlPath, invalidXml);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.IsNotNull(chips);
            Assert.AreEqual(0, chips.Count);
        }

        [TestMethod]
        public void LoadDatabase_MissingRootElement_ReturnsEmptyList()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<wrongroot>
    <SPI>
        <Winbond>
            <W25Q128FV id=""EF4018"" size=""16777216"" page=""256"" />
        </Winbond>
    </SPI>
</wrongroot>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.IsNotNull(chips);
            Assert.AreEqual(0, chips.Count);
        }

        [TestMethod]
        public void LoadDatabase_InvalidChipEntry_SkipsInvalidChip()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <Winbond>
            <W25Q128FV id=""EF4018"" size=""16777216"" page=""256"" />
            <BadChip id=""INVALID"" size=""notanumber"" page=""256"" />
            <W25Q64FV id=""EF4017"" size=""8388608"" page=""256"" />
        </Winbond>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert - Should load the 2 valid chips and skip the invalid one
            Assert.IsNotNull(chips);
            Assert.AreEqual(2, chips.Count);
            Assert.IsTrue(chips.Any(c => c.Name == "W25Q128FV"));
            Assert.IsTrue(chips.Any(c => c.Name == "W25Q64FV"));
            Assert.IsFalse(chips.Any(c => c.Name == "BadChip"));
        }

        [TestMethod]
        public void LoadDatabase_NullFilePath_UsesDefaultLocation()
        {
            // Act - Should not throw, but may return empty if no chiplist.xml in app directory
            var chips = ChipDatabaseLoader.LoadDatabase(null);

            // Assert
            Assert.IsNotNull(chips);
            // We can't guarantee the file exists, so just check it returns a list
        }

        [TestMethod]
        public void LoadDatabase_SSTPageTypes_ParsesCorrectly()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <SST>
            <SST25VF016B id=""BF2541"" size=""2097152"" page=""SSTB"" />
            <SST25VF040B id=""BF258D"" size=""524288"" page=""SSTW"" />
            <SST25VF080B id=""BF258E"" size=""1048576"" page=""256"" />
        </SST>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.AreEqual(3, chips.Count);
            Assert.AreEqual(-1, chips[0].PageSize); // SSTB
            Assert.AreEqual(-2, chips[1].PageSize); // SSTW
            Assert.AreEqual(256, chips[2].PageSize); // Normal
        }

        [TestMethod]
        public void LoadDatabase_DifferentManufacturers_ParsesCorrectly()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <Winbond>
            <W25Q128FV id=""EF4018"" size=""16777216"" page=""256"" />
        </Winbond>
        <Macronix>
            <MX25L3206E id=""C22016"" size=""4194304"" page=""256"" />
        </Macronix>
        <GigaDevice>
            <GD25Q64B id=""C84017"" size=""8388608"" page=""256"" />
        </GigaDevice>
        <Atmel>
            <AT25DF641 id=""1F4800"" size=""8388608"" page=""256"" />
        </Atmel>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.AreEqual(4, chips.Count);
            Assert.AreEqual(ChipManufacturer.Winbond, chips.First(c => c.Name == "W25Q128FV").Manufacturer);
            Assert.AreEqual(ChipManufacturer.Macronix, chips.First(c => c.Name == "MX25L3206E").Manufacturer);
            Assert.AreEqual(ChipManufacturer.GigaDevice, chips.First(c => c.Name == "GD25Q64B").Manufacturer);
            Assert.AreEqual(ChipManufacturer.Atmel, chips.First(c => c.Name == "AT25DF641").Manufacturer);
        }

        [TestMethod]
        public void LoadDatabase_CaseInsensitiveVoltage_ParsesCorrectly()
        {
            // Arrange - Test different case variations
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <Winbond>
            <W25Q80BW_1.8v id=""EF5014"" size=""1048576"" page=""256"" />
            <W25Q80BW_1.8V id=""EF5014"" size=""1048576"" page=""256"" />
            <W25Q80BW_1.8V id=""EF5014"" size=""1048576"" page=""256"" />
        </Winbond>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert - All should parse to 1.8V regardless of case
            Assert.AreEqual(3, chips.Count);
            Assert.IsTrue(chips.All(c => c.Voltage == 1800));
        }

        #endregion

        #region ValidateDatabase Tests

        [TestMethod]
        public void ValidateDatabase_ValidChips_ReturnsTrue()
        {
            // Arrange
            var chips = new List<ChipInfo>
            {
                new ChipInfo
                {
                    Name = "W25Q128FV",
                    Size = 16777216,
                    Manufacturer = ChipManufacturer.Winbond
                },
                new ChipInfo
                {
                    Name = "MX25L3206E",
                    Size = 4194304,
                    Manufacturer = ChipManufacturer.Macronix
                }
            };

            // Act
            var result = ChipDatabaseLoader.ValidateDatabase(chips);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ValidateDatabase_EmptyList_ReturnsFalse()
        {
            // Arrange
            var chips = new List<ChipInfo>();

            // Act
            var result = ChipDatabaseLoader.ValidateDatabase(chips);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateDatabase_NullList_ReturnsFalse()
        {
            // Act
            var result = ChipDatabaseLoader.ValidateDatabase(null);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateDatabase_ChipWithEmptyName_ReturnsFalse()
        {
            // Arrange
            var chips = new List<ChipInfo>
            {
                new ChipInfo
                {
                    Name = "",
                    Size = 16777216,
                    Manufacturer = ChipManufacturer.Winbond
                }
            };

            // Act
            var result = ChipDatabaseLoader.ValidateDatabase(chips);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateDatabase_ChipWithNullName_ReturnsFalse()
        {
            // Arrange
            var chips = new List<ChipInfo>
            {
                new ChipInfo
                {
                    Name = null,
                    Size = 16777216,
                    Manufacturer = ChipManufacturer.Winbond
                }
            };

            // Act
            var result = ChipDatabaseLoader.ValidateDatabase(chips);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateDatabase_ChipWithZeroSize_ReturnsFalse()
        {
            // Arrange
            var chips = new List<ChipInfo>
            {
                new ChipInfo
                {
                    Name = "W25Q128FV",
                    Size = 0,
                    Manufacturer = ChipManufacturer.Winbond
                }
            };

            // Act
            var result = ChipDatabaseLoader.ValidateDatabase(chips);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateDatabase_ChipWithNegativeSize_ReturnsFalse()
        {
            // Arrange
            var chips = new List<ChipInfo>
            {
                new ChipInfo
                {
                    Name = "W25Q128FV",
                    Size = -1000,
                    Manufacturer = ChipManufacturer.Winbond
                }
            };

            // Act
            var result = ChipDatabaseLoader.ValidateDatabase(chips);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateDatabase_MixedValidAndInvalid_ReturnsFalse()
        {
            // Arrange
            var chips = new List<ChipInfo>
            {
                new ChipInfo
                {
                    Name = "W25Q128FV",
                    Size = 16777216,
                    Manufacturer = ChipManufacturer.Winbond
                },
                new ChipInfo
                {
                    Name = "",
                    Size = 0,
                    Manufacturer = ChipManufacturer.Other
                }
            };

            // Act
            var result = ChipDatabaseLoader.ValidateDatabase(chips);

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region Manufacturer Parsing Tests

        [TestMethod]
        public void LoadDatabase_UnknownManufacturer_DefaultsToOther()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <UnknownBrand>
            <CHIP123 id=""FF0000"" size=""1048576"" page=""256"" />
        </UnknownBrand>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.AreEqual(1, chips.Count);
            Assert.AreEqual(ChipManufacturer.Other, chips[0].Manufacturer);
        }

        [TestMethod]
        public void LoadDatabase_ManufacturerAliases_ParsesCorrectly()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <W>
            <CHIP1 id=""EF0000"" size=""1048576"" page=""256"" />
        </W>
        <MXIC>
            <CHIP2 id=""C20000"" size=""1048576"" page=""256"" />
        </MXIC>
        <GD>
            <CHIP3 id=""C80000"" size=""1048576"" page=""256"" />
        </GD>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.AreEqual(3, chips.Count);
            Assert.AreEqual(ChipManufacturer.Winbond, chips[0].Manufacturer);
            Assert.AreEqual(ChipManufacturer.Macronix, chips[1].Manufacturer);
            Assert.AreEqual(ChipManufacturer.GigaDevice, chips[2].Manufacturer);
        }

        #endregion

        #region JEDEC ID Parsing Tests

        [TestMethod]
        public void LoadDatabase_ValidJedecId_ParsesCorrectly()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <Winbond>
            <W25Q128FV id=""EF4018"" size=""16777216"" page=""256"" />
        </Winbond>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.AreEqual(1, chips.Count);
            Assert.AreEqual(0xEF, chips[0].ManufacturerId);
            Assert.AreEqual(0x4018, chips[0].DeviceId);
        }

        [TestMethod]
        public void LoadDatabase_MissingJedecId_DefaultsToZero()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <Winbond>
            <W25Q128FV size=""16777216"" page=""256"" />
        </Winbond>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.AreEqual(1, chips.Count);
            Assert.AreEqual(0, chips[0].ManufacturerId);
            Assert.AreEqual(0, chips[0].DeviceId);
        }

        [TestMethod]
        public void LoadDatabase_InvalidJedecId_DefaultsToZero()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <Winbond>
            <W25Q128FV id=""INVALID"" size=""16777216"" page=""256"" />
        </Winbond>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.AreEqual(1, chips.Count);
            Assert.AreEqual(0, chips[0].ManufacturerId);
            Assert.AreEqual(0, chips[0].DeviceId);
        }

        #endregion

        #region SPI Command Set Tests

        [TestMethod]
        public void LoadDatabase_DifferentSpiCommandSets_ParsesCorrectly()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <Winbond>
            <CHIP25 id=""EF0000"" size=""1048576"" page=""256"" spicmd=""25"" />
            <CHIP45 id=""1F0000"" size=""1048576"" page=""256"" spicmd=""45"" />
            <CHIP95 id=""200000"" size=""1048576"" page=""256"" spicmd=""95"" />
            <CHIPKB id=""300000"" size=""1048576"" page=""256"" spicmd=""KB"" />
        </Winbond>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.AreEqual(4, chips.Count);
            Assert.AreEqual(SpiCommandSet.Series25, chips[0].SpiCommandSet);
            Assert.AreEqual(SpiCommandSet.Series45, chips[1].SpiCommandSet);
            Assert.AreEqual(SpiCommandSet.Series95, chips[2].SpiCommandSet);
            Assert.AreEqual(SpiCommandSet.KB9012, chips[3].SpiCommandSet);
        }

        [TestMethod]
        public void LoadDatabase_MissingSpiCommandSet_DefaultsToSeries25()
        {
            // Arrange
            var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<chiplist>
    <SPI>
        <Winbond>
            <W25Q128FV id=""EF4018"" size=""16777216"" page=""256"" />
        </Winbond>
    </SPI>
</chiplist>";
            File.WriteAllText(_testXmlPath, xmlContent);

            // Act
            var chips = ChipDatabaseLoader.LoadDatabase(_testXmlPath);

            // Assert
            Assert.AreEqual(1, chips.Count);
            Assert.AreEqual(SpiCommandSet.Series25, chips[0].SpiCommandSet);
        }

        #endregion
    }
}
