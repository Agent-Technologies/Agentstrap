 [Agent Technologies Developer Application](https://forms.gle/8VxwBFtKLncau4h59)

 Please record your email.

> [!CAUTION]
> The only official place to download Agentstrap is this GitHub repository and the
> [Agentstrap documentation site](https://agentstrap-doc.pages.dev). Any other website offering downloads or
> claiming to be us is not owned by us.


<div align="center">

# Agentstrap

**A third-party replacement for the standard Roblox bootstrapper, with a lot more going on.**

[![License](https://img.shields.io/github/license/Agent-Technologies/Agentstrap)](https://github.com/Agent-Technologies/Agentstrap/blob/main/LICENSE)
[![Downloads](https://img.shields.io/github/downloads/Agent-Technologies/Agentstrap/latest/total?color=981bfe)](https://github.com/Agent-Technologies/Agentstrap/releases)
[![Version](https://img.shields.io/github/v/release/Agent-Technologies/Agentstrap?color=7a39fb)](https://github.com/Agent-Technologies/Agentstrap/releases/latest)

</div>

</div>

<p align="center">
<img src="assets/AgentstrapUI.png" alt="Agentstrap interface">
</p>

Agentstrap takes over the job Roblox's own launcher does and uses that position to add things Roblox doesn't offer: a matchmaker, an in-game server overlay, a diagnostics suite, deployment channel pinning, modding that survives updates, and more.

It is a fork of Bloxstrap, and everything Bloxstrap does, Agentstrap does. (Bloxstrap support and development have ended. Agentstrap is a full Roblox bootstrapper, though!)

Running into a problem or need help with something? The documentation is available in docs/ in this repository and online at the documentation site. If you can't find what you're looking for or would like to suggest something, please submit an issue.

Agentstrap is only supported on PCs running Windows.

# Features
## New in Agentstrap

Matchmaker - Automatically join a place on launch, optionally preferring servers that aren't already full.

Command Center - Launch, update, and repair Roblox from a single screen, with live status for every part of the installation.

Server Connection Overlay - A toast notification on the game window telling you which server you connected to and where its data center is located.

Diagnostics - Eight health checks that identify broken installations, bad permissions, conflicting mods, and pending configuration migrations.

Deployment Channels - Pin Agentstrap to a specific Roblox release channel instead of following production.

Custom Window Attributes - Force a fixed size, position, and title on the Roblox window every time.

Updates Page - Independently check for updates to Agentstrap, your installed mods, and your configuration.

NEXUS Mode - An opt-in experimental feature channel.

Global Settings - Save Roblox settings in Agentstrap and have them applied to Roblox. :)
From Bloxstrap

Discord Rich Presence - Hassle-free Discord Rich Presence, so your friends know what you're playing at a glance.

Content Modding - Modify content files such as cursors, sounds, emoji fonts, custom fonts, and skyboxes. Your modifications survive Roblox updates.

Server Location Hints - See server location information, courtesy of ipinfo.io.

Fast Flag Management - Manage Fast Flags with rendering presets, preset bundles, and a built-in flag validator.

Customization - Custom themes, custom shortcuts, and a configurable bootstrapper.

Full documentation for all these features is available in docs/.


# Installing

Download the latest release of Agentstrap and run it. The installer asks where you want the app to be installed and whether you want desktop and Start Menu shortcuts. That's about it!

Agentstrap targets .NET 10, and the published builds are self-contained, so you don't need to install a separate runtime.

It's not unlikely that Windows SmartScreen will show a popup when you run Agentstrap for the first time. This happens because it's an unknown program, not because it has actually been detected as malicious. To dismiss it, click More info and then Run anyway.

Once installed, Agentstrap is added to your Start Menu, where you can access the menu and reconfigure your preferences.

# Frequently Asked Questions

Q: Is this malware?

A: No. The source code here is publicly available, and it would be difficult to slip anything malicious into the downloads without anyone noticing. Just make sure you're downloading from an official source — this GitHub repository is the only one.

Q: Can using this get me banned?

A: No, it shouldn't. Agentstrap doesn't interact with the Roblox client in the same way that exploits do. It changes how Roblox is launched and what gets copied into the installation folder, not the client itself.

Q: Does Agentstrap send my data anywhere?

A: There's no analytics backend. The only outbound requests are Roblox downloads, GitHub release checks, and — if you enable them — ipinfo.io server location lookups and per-mod update checks.

# Code

Agentstrap uses the WPF UI library for its user interface, vendored as a submodule in wpfui/.

powershell
git clone --recurse-submodules https://github.com/Agent-Technologies/Agentstrap
dotnet build Agentstrap/Agentstrap.csproj -c Release


If you cloned without --recurse-submodules, run git submodule update --init first. The build will fail without it.

To publish a self-contained x64 build:

powershell
dotnet publish Agentstrap/Agentstrap.csproj -p:PublishProfile=Publish-x64 -c Release


There's also a test project alongside the app:

powershell
dotnet test Agentstrap.Tests/Agentstrap.Tests.csproj

# Credits

Agentstrap is a fork of Bloxstrap by pizzaboxer, and it would not exist without it. Okay, maybe it would, but Agentstrap would be WAY worse without Bloxstrap.

Anyway, Roblox and Roblox Studio are trademarks of Roblox Corporation.

### Contributors

AgentXCO4, ByeBendy
