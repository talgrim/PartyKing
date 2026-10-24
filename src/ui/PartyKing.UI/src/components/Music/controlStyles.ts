/** Every playback control is fully rounded, so the volume control, Play/Pause and Next song match. */
export const roundedClass = 'rounded-full';

/** The slate surface behind a control. */
export const pillClass = `inline-flex h-10 items-center ${roundedClass} bg-slate-700`;

/** An icon-only round button: same surface as `pillClass`, with a hover state and a disabled state. */
export const iconButtonClass = `${pillClass} w-10 justify-center text-slate-200 cursor-pointer hover:bg-slate-600 disabled:opacity-50 disabled:cursor-default disabled:hover:bg-slate-700`;
