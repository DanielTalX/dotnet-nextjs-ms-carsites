'use client'

import { Button } from 'flowbite-react'
import { User } from 'next-auth'
import Link from 'next/link'
import React from 'react'

type Props = {
  user: Partial<User>
}

export default function UserActions({user}: Props) {
    return  (
        <Button outline>
            <Link href='/session'>
                Session
            </Link>

        </Button>
    )
}