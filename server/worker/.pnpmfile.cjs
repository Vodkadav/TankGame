// Allow build scripts for native dependencies required by workerd/wrangler.
function readPackage(pkg) {
  return pkg;
}

module.exports = { hooks: { readPackage } };
