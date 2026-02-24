# ForgeX

A Halo 3 / Reach Forge usermap editor for Xbox 360 and MCC formats.

## Supported Formats

- **Xbox 360 STFS containers** (CON/LIVE/PIRS) - Full read/write
- **MCC Halo 3 BLF (.mvar)** - Packed and unpacked, full read/write
- **MCC Halo 3 compressed (.mvar)** - Auto-decompression, full read/write
- **MCC Halo: Reach BLF (.mvar)** - Read-only

## Building

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
dotnet build
dotnet run --project ForgeX.Desktop
```

## Running Tests

```
dotnet test
```

## Credits

- **Jestzer** - Creator
- **Supermodder911** - Original Forge editor
- **Lord Zedd** - Research and documentation
- **Assembly contributors** - Halo modding tooling

## License

This project is licensed under the GNU General Public License v3.0. See [LICENSE](LICENSE) for details.
