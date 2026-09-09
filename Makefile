# twxproxy 3.0 — developer targets
# Requires .NET SDK 10.x (installed at ~/.dotnet by the no-sudo install script)

DOTNET_DIR ?= $(HOME)/.dotnet
DOTNET := $(DOTNET_DIR)/dotnet

.SUFFIXES:
.PHONY: build debug clean

build:
	$(DOTNET) build Source/TWXProxy.sln
	$(DOTNET) build Source/TWXP/TWXP.csproj

# Live debug run of the MTC desktop client: edits auto-rebuild and relaunch
# the app (dotnet watch). Ctrl-C to stop.
debug:
	DOTNET_ROOT=$(DOTNET_DIR) $(DOTNET) watch run --project Source/MTC/MTC.csproj

clean:
	$(DOTNET) clean Source/TWXProxy.sln
	$(DOTNET) clean Source/TWXP/TWXP.csproj
