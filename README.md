# ForgeX

A Halo Forge usermap editor for Xbox 360 and MCC formats.

## Supported Formats

| Format | Read | Write |
|--------|------|-------|
| Xbox 360 STFS containers (CON/LIVE/PIRS) | Yes | Yes |
| MCC Halo 3 BLF (.mvar) - packed and unpacked | Yes | Yes |
| MCC Halo 3 compressed (.mvar) | Yes | Yes (auto-decompression) |
| MCC Halo: Reach BLF (.mvar) | Yes | Yes |
| MCC Halo 4 BLF (.mvar) | Yes | No |

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
- **Supermodder911** - Original creator of the Forge program for Xbox 360 Halo 3 usermaps
- **Lord Zedd** - Vast majority of research and documentation used to make this possible
- **Other Assembly and Forge contributors**
- **Other contributors to the modding community whole made this possible**

## License

This project is licensed under the GNU General Public License v3.0. See [LICENSE](LICENSE) for details.
