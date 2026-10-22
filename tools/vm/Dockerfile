FROM dockurr/windows@sha256:16fd7c39a3e48bb946d29c59dc6015942e5b900a1fa5d7e5552053bfc48ee2ea
RUN apt-get update -qq && apt-get install -y --no-install-recommends smbclient && rm -rf /var/lib/apt/lists/*
# Retain UAC and the firewall in the guest so per-user Setup is tested under
# normal Windows security settings. The host's settings are never changed.
RUN sed -i 's/<EnableLUA>false<\/EnableLUA>/<EnableLUA>true<\/EnableLUA>/;s/<EnableFirewall>false<\/EnableFirewall>/<EnableFirewall>true<\/EnableFirewall>/' /run/assets/win10x64-enterprise.xml
