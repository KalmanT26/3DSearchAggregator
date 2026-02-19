
import { useId } from 'react';

export default function Logo({ className = "", size = 40 }: { className?: string; size?: number }) {
    // Unique IDs for gradients to prevent conflicts if multiple logos are rendered
    const id = useId();
    const gradientId1 = `${id}-gradient-1`;
    const gradientId2 = `${id}-gradient-2`;
    const gradientId3 = `${id}-gradient-3`;
    const gloWId = `${id}-glow`;

    return (
        <svg
            width={size}
            height={size}
            viewBox="0 0 40 40"
            fill="none"
            xmlns="http://www.w3.org/2000/svg"
            className={className}
        >
            <defs>
                <filter id={gloWId} x="-20%" y="-20%" width="140%" height="140%">
                    <feGaussianBlur stdDeviation="2" result="blur" />
                    <feComposite in="SourceGraphic" in2="blur" operator="over" />
                </filter>
                <linearGradient id={gradientId1} x1="20" y1="4" x2="20" y2="20" gradientUnits="userSpaceOnUse">
                    <stop stopColor="#3EB8FF" />
                    <stop offset="1" stopColor="#0072FF" />
                </linearGradient>
                <linearGradient id={gradientId2} x1="6" y1="12" x2="20" y2="36" gradientUnits="userSpaceOnUse">
                    <stop stopColor="#0056D2" />
                    <stop offset="1" stopColor="#003580" />
                </linearGradient>
                <linearGradient id={gradientId3} x1="34" y1="12" x2="20" y2="36" gradientUnits="userSpaceOnUse">
                    <stop stopColor="#2A9DFF" />
                    <stop offset="1" stopColor="#0060E0" />
                </linearGradient>
            </defs>
            
            <g filter={`url(#${gloWId})`}>
                {/* Top Face */}
                <path 
                    d="M20 4L34 12L20 20L6 12L20 4Z" 
                    fill={`url(#${gradientId1})`} 
                    stroke="rgba(255,255,255,0.1)" 
                    strokeWidth="1"
                />
                
                {/* Left Face */}
                <path 
                    d="M6 12L20 20V36L6 28V12Z" 
                    fill={`url(#${gradientId2})`} 
                    stroke="rgba(255,255,255,0.05)" 
                    strokeWidth="1"
                />
                
                {/* Right Face */}
                <path 
                    d="M34 12L34 28L20 36V20L34 12Z" 
                    fill={`url(#${gradientId3})`} 
                    stroke="rgba(255,255,255,0.05)" 
                    strokeWidth="1"
                />

                {/* Inner stylized 'M' or structure accent */}
                <path 
                    d="M20 20L20 36" 
                    stroke="rgba(62, 184, 255, 0.4)" 
                    strokeWidth="1"
                />
                <path 
                    d="M20 20L6 12" 
                    stroke="rgba(62, 184, 255, 0.4)" 
                    strokeWidth="1"
                />
                <path 
                    d="M20 20L34 12" 
                    stroke="rgba(62, 184, 255, 0.4)" 
                    strokeWidth="1"
                />
            </g>
        </svg>
    );
}
