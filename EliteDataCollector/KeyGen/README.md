# Elite Data Collector - Key Generator

A utility for generating authentication keys for the Elite Data Collector application.

## Features

- Generate multiple authentication keys at once
- Pre-configured key format: `KEY-CMDR[9-digit-id]-[checksum]`
- Automatic checksum generation using SHA256
- Saves all generated keys to a timestamped text file
- Standalone executable (no .NET installation required)

## Usage

### Method 1: Batch File (Recommended)
Double-click `run.bat` to launch the key generator. When prompted, enter the number of keys you want to generate.

```
C:\path\to\KeyGen> run.bat
```

### Method 2: Command Line
Run with custom key count:
```
C:\path\to\KeyGen> KeyGen.exe
```

### Method 3: PowerShell
```powershell
PS> C:\path\to\KeyGen\KeyGen.exe
```

## Example Output

```
╔════════════════════════════════════════════════════════╗
║   Elite Data Collector - Key Generator                ║
╚════════════════════════════════════════════════════════╝

How many keys do you want to generate? 5

Generating keys...

KEY-CMDR000123456-2F8A5D91
KEY-CMDR789456123-7C1D4E2B
KEY-CMDR456789012-9B3F6E1C
KEY-CMDR012345789-4A7B9D2F
KEY-CMDR567890123-C5E1F9A2

✓ Keys saved to: keys_2026-03-24_21-30-15.txt
✓ Key generation complete!
```

## Key Format

Each generated key follows the format:
- **Prefix**: `KEY-CMDR`
- **ID**: 9-digit random commander ID (000000000 to 999999999)
- **Checksum**: 8-character hexadecimal hash (validates key integrity)

Example: `KEY-CMDR000000123-ABC123EF`

## Output Files

Generated keys are automatically saved to a text file with the pattern:
```
keys_YYYY-MM-DD_HH-mm-ss.txt
```

The file contains:
- Generation timestamp
- Total number of keys generated
- All generated keys (one per line)

## Building from Source

To rebuild the application:

```bash
cd EliteDataCollector\KeyGen
dotnet build -c Release
```

To create a standalone executable:

```bash
cd EliteDataCollector\KeyGen
dotnet publish -c Release -o publish --self-contained
```

## System Requirements

- **Option 1**: .NET 10 Desktop Runtime (if running from source)
- **Option 2**: None - the standalone executable includes everything needed

## Files

- `KeyGen.exe` - Standalone executable (compiled release build)
- `Program.cs` - Application source code
- `KeyGen.csproj` - Project configuration
- `run.bat` - Batch file launcher
- `README.md` - This file

## Notes

- The checksum is deterministic: the same commander ID always produces the same checksum
- Keys are generated with cryptographically secure random IDs
- No network access or external dependencies required
- All operations are performed locally on your machine

---

**Part of Elite Data Collector** - A production-ready Windows application for monitoring Elite Dangerous gameplay.
