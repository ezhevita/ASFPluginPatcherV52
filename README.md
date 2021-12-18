# ASF Plugin Patcher v5.2
Patches all the loaded plugins in order to work after ASF V5.2.1.0 breaking changes.
### This plugin does not work in vanilla ASF since it stops loading plugins if at least one can't be loaded!

## How to use
1. Put all the files from a release archive to the `plugins` directory of ASF
2. Start ASF, plugin will detect and load all the broken plugins and will migrate it to new version
3. ASF will be restarted and all the plugins should load correctly
4. Done!
