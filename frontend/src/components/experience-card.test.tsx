// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { ExperienceCard } from '../pages/public'

describe('public experience card', () => {
  it('keeps a hidden company hidden and links to the real detail route', () => {
    render(<MemoryRouter><ExperienceCard item={{ id: 'a1', kind: 'interviewer', interviewDate: '2026-11-17', level: 'Senior', authorName: null, companyName: null, interviewRating: 4, interviewerConductRating: null, jobAdAlignmentRating: null }} /></MemoryRouter>)
    expect(screen.getByText('شرکت پنهان')).toBeTruthy()
    expect(screen.getByRole('link').getAttribute('href')).toBe('/experiences/interviewer/a1')
    expect(screen.getByText('ناشناس')).toBeTruthy()
  })
})
