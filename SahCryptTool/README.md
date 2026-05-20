# SahCryptTool

This folder contains the C++ ESAH helper tool source.

Format:

```txt
ESAH + AES-256-GCM
```

The included key is a public sample key:

```txt
0123456789ABCDEF0123456789ABCDEF
```

Before production use, replace the sample key in `main.cpp` with the same 32-byte production key used by server-side `launcher.ini`.
