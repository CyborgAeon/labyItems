1. install mongosh for mongo shell commands.
1. 1. this can be done using `brew install mongosh` or by going to mongo and finding the download button, adjusting your env variables etc as normal
1. install docker from: `https://docs.docker.com/desktop/setup/install/windows-install/` or `https://docs.docker.com/desktop/setup/install/mac-install/`
1. ensure CLI is working with docker --version
1. 1. if you need to enable virtualization and don't know about it, google it!
1. pull mongo image `docker pull mongodb/mongodb-atlas-local:latest`
1. run mongo in a container `docker run --name laby-items -p 27018:27017 mongodb/mongodb-atlas-local`
1. 1. if you already have mongo running in another container, remember it has a default routing to `27017`, so use the same image or turn off your existing one.
1. 1. check your image is running in a container...
1. connect to your mongo shell `mongosh "mongodb://localhost:27018"`
## setup dotnet
1. run these to download .net cli `curl -L https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh`; `chmod +x dotnet-install.sh`; `./dotnet-install.sh --channel 8.0`
1. 1. we're using 8 for .net maui
1. run this to add dotnet to path `export PATH="$HOME/.dotnet:$PATH"`
1. run `dotnet --info` to check above worked
1. run `dotnet workload install maui` to install maui workload
1. run `dotnet workload install maui-android` to install android workload




# Add it to PATH (add this line to your shell profile too: ~/.zshrc)
export PATH="$HOME/.dotnet:$PATH"