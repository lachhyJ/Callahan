import { useEffect, useState } from 'react'
import { buildInfoLabel } from '../buildInfo'
import { getNativeStatus, nativeBuildTag } from '../nativeInfo'

// Diagnostic only — which deploy the webview is running, and (native shell
// only) which git branch/commit Xcode actually built. Tucked at the bottom
// of the Dashboard rather than the TopBar: useful when you go looking for
// it, not something that needs to compete for attention on every screen.
export default function BuildFooter() {
  const [nativeStatus, setNativeStatus] = useState(null)

  useEffect(() => { getNativeStatus().then(setNativeStatus) }, [])

  const web = buildInfoLabel()
  if (!web && !nativeStatus) return null

  return (
    <div className="build-footer section-gap">
      {web && <span className="build-tag" title={web}>{web}</span>}
      {nativeStatus && <span className="build-tag" title={nativeBuildTag(nativeStatus)}>{nativeBuildTag(nativeStatus)}</span>}
    </div>
  )
}
