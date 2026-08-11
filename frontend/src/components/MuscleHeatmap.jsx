// Simplified body silhouette, not anatomically precise — just enough regions
// to place each tracked muscle group somewhere recognizable. Intensity is a
// single-hue (accent) light-to-dark ramp against set count, the correct
// encoding for a magnitude view (never a rainbow of one-hue-per-region).
// Segments that stack (shoulder/bicep, chest/core, quad/calf) share an exact
// boundary with no rounding at the seam, so they read as one continuous limb
// instead of floating blocks — only the outer caps are rounded.
const MAX_MIX = 85
const MIN_MIX = 20
const NEUTRAL = 'var(--surface-raised)'

function intensity(setCount, maxCount) {
  if (!setCount) return NEUTRAL
  const ratio = maxCount > 0 ? setCount / maxCount : 0
  const mix = MIN_MIX + ratio * (MAX_MIX - MIN_MIX)
  return `color-mix(in srgb, var(--accent) ${mix.toFixed(0)}%, var(--surface-raised))`
}

function Seg({ x, y, w, h, fill, rTop, rBottom, label }) {
  const rt = rTop ?? 0
  const rb = rBottom ?? 0
  const d = `M${x} ${y + rt}
    a${rt} ${rt} 0 0 1 ${rt} ${-rt}
    h${w - 2 * rt}
    a${rt} ${rt} 0 0 1 ${rt} ${rt}
    v${h - rt - rb}
    a${rb} ${rb} 0 0 1 ${-rb} ${rb}
    h${-(w - 2 * rb)}
    a${rb} ${rb} 0 0 1 ${-rb} ${-rb}
    Z`
  return <path d={d} fill={fill} stroke="var(--border)" strokeWidth="0.5" aria-label={label} />
}

function Figure({ title, torsoTop, torsoBottom, armTop, armBottom, legTop, legBottom, calfBottom }) {
  return (
    <svg viewBox="0 0 100 190" className="muscle-figure" role="img" aria-label={title}>
      {/* head + neck */}
      <circle cx="50" cy="10" r="9" fill={NEUTRAL} stroke="var(--border)" strokeWidth="0.5" />
      <rect x="45" y="17" width="10" height="7" fill={NEUTRAL} />

      {/* arms: shoulder cap + upper-arm segment, flush at the elbow-adjacent seam */}
      <Seg x={12} y={24} w={13} h={18} rTop={6} fill={torsoTop.shoulderFill} label="Shoulders" />
      <Seg x={12} y={42} w={13} h={38} rBottom={6} fill={armBottom.fill} label={armBottom.label} />
      <Seg x={75} y={24} w={13} h={18} rTop={6} fill={torsoTop.shoulderFill} label="Shoulders" />
      <Seg x={75} y={42} w={13} h={38} rBottom={6} fill={armBottom.fill} label={armBottom.label} />

      {/* torso: chest/back segment + core/lower-back segment, flush seam */}
      <Seg x={30} y={24} w={40} h={torsoTop.h} rTop={7} fill={torsoTop.fill} label={torsoTop.label} />
      <Seg x={30} y={torsoTop.bottomY} w={40} h={torsoBottom.h} fill={torsoBottom.fill} label={torsoBottom.label} />

      {/* legs: thigh segment + calf segment, flush seam */}
      <Seg x={30} y={legTop.y} w={16} h={legTop.h} fill={legTop.fill} label={legTop.label} />
      <Seg x={54} y={legTop.y} w={16} h={legTop.h} fill={legTop.fill} label={legTop.label} />
      <Seg x={30} y={legBottom.y} w={16} h={legBottom.h} rBottom={5} fill={legBottom.fill} label="Calves" />
      <Seg x={54} y={legBottom.y} w={16} h={legBottom.h} rBottom={5} fill={legBottom.fill} label="Calves" />

      <ellipse cx="38" cy={legBottom.y + legBottom.h + 3} rx="7" ry="3.5" fill={NEUTRAL} stroke="var(--border)" strokeWidth="0.5" />
      <ellipse cx="62" cy={legBottom.y + legBottom.h + 3} rx="7" ry="3.5" fill={NEUTRAL} stroke="var(--border)" strokeWidth="0.5" />
    </svg>
  )
}

export default function MuscleHeatmap({ balance }) {
  const maxCount = Math.max(...balance.map((b) => b.setCount), 1)
  const colorFor = (group) => intensity(balance.find((b) => b.muscleGroup === group)?.setCount ?? 0, maxCount)

  const shoulderFill = colorFor('Shoulders')

  return (
    <div className="muscle-heatmap">
      <Figure
        title="Front view"
        torsoTop={{ shoulderFill, fill: colorFor('Chest'), label: 'Chest', h: 30, bottomY: 54 }}
        torsoBottom={{ fill: colorFor('Core'), label: 'Core', h: 38 }}
        armBottom={{ fill: colorFor('Biceps'), label: 'Biceps' }}
        legTop={{ y: 92, h: 46, fill: colorFor('Quads'), label: 'Quads' }}
        legBottom={{ y: 138, h: 36, fill: colorFor('Calves') }}
      />
      <Figure
        title="Back view"
        torsoTop={{ shoulderFill, fill: colorFor('Back'), label: 'Back', h: 46, bottomY: 70 }}
        torsoBottom={{ fill: colorFor('Glutes'), label: 'Glutes', h: 22 }}
        armBottom={{ fill: colorFor('Triceps'), label: 'Triceps' }}
        legTop={{ y: 92, h: 46, fill: colorFor('Hamstrings'), label: 'Hamstrings' }}
        legBottom={{ y: 138, h: 36, fill: colorFor('Calves') }}
      />
    </div>
  )
}
